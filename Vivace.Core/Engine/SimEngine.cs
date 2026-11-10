using System.Collections.Immutable;
using System.Threading.Channels;
using Vivace.Core.Decision;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Planning;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;
using Vivace.Core.Rules;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Core.Engine;

/// <summary>シミュレーションのエンジン。状態を書く唯一の場所で、イベントを時刻、段、連番の順に適用し、決める側の出力を取り込む。</summary>
public sealed class SimEngine : IAsyncDisposable
{
    private readonly RuleContext _ctx;
    private readonly IDecider _decider;
    private readonly IStorage _storage;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    private readonly Lanes _lanes = new();
    /// <summary>未適用のイベント。時刻、段、連番の順に取り出す。</summary>
    private readonly PriorityQueue<SimEvent, (DateTimeOffset Time, int Stage, long Seq)> _queue = new();
    private readonly Dictionary<long, Running> _running = [];
    private readonly List<Output> _held = [];
    private readonly HashSet<long> _dropped = [];
    private readonly Channel<Output> _outputs = Channel.CreateUnbounded<Output>();
    private readonly List<LoggedEvent> _log = [];
    private long _nextSeq;
    private long _applyNo;
    private int _sameInstantStreak;
    private SimState _state;

    /// <summary>初期の状態と部品から、エンジンを作る。</summary>
    private SimEngine(RuleContext ctx, IDecider decider, IStorage storage, IRandomSource random, IClock clock)
    {
        _ctx = ctx;
        _decider = decider;
        _storage = storage;
        _random = random;
        _clock = clock;
        _state = new SimState(
            new Position(clock.UtcNow, EventStage.DayTurn, 0, 0),
            ImmutableDictionary<WorldId, WorldState>.Empty,
            ImmutableDictionary<PersonaId, PersonaState>.Empty);
    }

    /// <summary>エンジンを作る。</summary>
    /// <param name="definitions">読み込んだ定義。</param>
    /// <param name="storage">保存先。</param>
    /// <param name="random">乱数の列を開く口。</param>
    /// <param name="clock">時計。イベントの時刻まで待ってから適用する。</param>
    /// <param name="decider">自由時間の決定。省略すると UtilityDecider。</param>
    /// <param name="holidays">祝日の暦。省略すると祝日のない暦。</param>
    public static SimEngine Create(Definitions.Definitions definitions, IStorage storage, IRandomSource random, IClock clock, IDecider? decider = null, IHolidayCalendar? holidays = null) =>
        new(new RuleContext(definitions, RouteCache.Create(definitions), holidays ?? NoHolidays.Instance), decider ?? new UtilityDecider(), storage, random, clock);

    /// <summary>いまの状態（不変）。</summary>
    public SimState State => _state;

    /// <summary>適用の記録（適用された順）。</summary>
    public IReadOnlyList<LoggedEvent> Log => _log;

    /// <summary>イベントをキューに入れる。時刻は、いまより前にできない。走っている間は呼べない。</summary>
    /// <param name="ev">投入するイベント。</param>
    /// <param name="origin">出所。</param>
    /// <returns>割り当てた連番。</returns>
    public long Inject(NewEvent ev, Origin origin)
    {
        if (ev.Time < _clock.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(ev), "過去の時刻は投入できない");
        }

        return Enqueue(ev, origin, null);
    }

    /// <summary>
    /// 時刻 until まで、イベントを適用し、決める側の仕事の結果を取り込む。until より後のイベントは、キューに残す。
    /// </summary>
    /// <param name="until">進める先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task RunUntilAsync(DateTimeOffset until, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            DrainOutputs();
            var head = _queue.TryPeek(out var next, out _) ? next : null;
            var limit = head?.Time ?? DateTimeOffset.MaxValue;
            if (_running.Values.Any(run => run.Horizon <= limit))
            {
                Receive(await _outputs.Reader.ReadAsync(ct));
                continue;
            }

            if (_held.Any(output => output.Horizon <= limit))
            {
                InsertHeld(limit);
                continue;
            }

            if (head is null || head.Time > until)
            {
                break;
            }

            _queue.Dequeue();
            await ApplyAsync(head, ct);
        }

        if (_clock.UtcNow < until)
        {
            await _clock.WaitUntilAsync(until, ct);
        }
    }

    /// <summary>レーンを止める。動いている仕事は、結果を捨てる。</summary>
    public ValueTask DisposeAsync()
    {
        _lanes.Complete();
        return _storage.DisposeAsync();
    }

    /// <summary>1つのイベントを適用する。不変条件を検査してからコミットし、シグナルを実行する。</summary>
    private async Task ApplyAsync(SimEvent ev, CancellationToken ct)
    {
        var previous = _state.Position.Time;
        await _clock.WaitUntilAsync(ev.Time, ct);
        var result = Rules.Rules.Apply(_ctx, _state, ev);
        long? applyNo = null;
        if (result.Outcome == EventOutcome.Applied)
        {
            applyNo = ++_applyNo;
            _state = result.State with { Position = new Position(ev.Time, ev.Stage, ev.Seq, applyNo.Value) };
        }
        else
        {
            _state = result.State;
        }

        var status = result.Outcome switch
        {
            EventOutcome.Applied => EventStatus.Applied,
            EventOutcome.Skipped => EventStatus.Skipped,
            _ => EventStatus.Rejected,
        };
        var logged = new LoggedEvent(ev, status, applyNo, result.Reason);
        _log.Add(logged);
        foreach (var followUp in result.FollowUps)
        {
            Enqueue(followUp, ev.Origin, ev.Seq);
        }

        if (result.Outcome == EventOutcome.Applied)
        {
            _sameInstantStreak = ev.Time == previous ? _sameInstantStreak + 1 : 0;
            Invariants.CheckProgress(_sameInstantStreak);
            Invariants.Check(_ctx, _state, previous, BusyAfter(result.Signals), PendingEvents(), EarliestPending());
        }

        await _storage.CommitAsync(new StorageBatch([logged], _state), ct);
        foreach (var signal in result.Signals)
        {
            Handle(signal, ev.Origin);
        }
    }

    /// <summary>適用の後に動いている決める仕事のペルソナ。これから起動するシグナルの分も含める。</summary>
    private ImmutableHashSet<PersonaId> BusyAfter(ImmutableArray<EngineSignal> signals) =>
        Busy().Union(signals.Select(signal => signal switch
        {
            StartDecider decide => decide.Persona,
            StartPlanner plan => plan.Persona,
            _ => (PersonaId?)null,
        }).Where(persona => persona is not null).Select(persona => persona!.Value));

    /// <summary>後続のイベントに連番と出所を付けて、キューに入れる。</summary>
    private long Enqueue(NewEvent ev, Origin origin, long? cause)
    {
        var seq = ++_nextSeq;
        var stage = EventTypes.StageOf(ev.EventType);
        var simEvent = new SimEvent(
            seq,
            ev.Time,
            ev.EventType,
            ev.World,
            ev.Persona,
            ev.Payload,
            ev.Guard,
            ev.BatchSeq,
            cause,
            ev.Origin ?? origin,
            null,
            null);
        _queue.Enqueue(simEvent, (ev.Time, (int)stage, seq));
        return seq;
    }

    /// <summary>シグナルを実行する。</summary>
    private void Handle(EngineSignal signal, Origin origin)
    {
        switch (signal)
        {
            case StartDecider decide:
                var decideSnapshot = _state;
                StartHandler(decide.BatchKey, decide.Persona, decide.Now, $"decide:{decide.Persona}", origin, () => Decide(decide, decideSnapshot));
                break;
            case StartPlanner plan:
                var planSnapshot = _state;
                StartHandler(plan.BatchKey, plan.Persona, plan.Now, $"plan:{plan.Persona}", origin, () => Plan(plan, planSnapshot));
                break;
            case CancelHandler cancel:
                foreach (var run in _running.Values.Where(run => run.Persona == cancel.Persona && IsCancellable(run.Lane)).ToList())
                {
                    _running.Remove(run.Key);
                    _dropped.Add(run.Key);
                }

                break;
        }
    }

    /// <summary>取り消しの対象になるレーンか。決める側のレーンだけを止め、Planner は止めない。</summary>
    /// <param name="lane">レーンの鍵。</param>
    internal static bool IsCancellable(string lane) => lane.StartsWith("decide:", StringComparison.Ordinal);

    /// <summary>決める側の仕事を、レーンで起動する。仕事は写し（起動の時点の状態）だけを読む。</summary>
    private void StartHandler(long key, PersonaId persona, DateTimeOffset horizon, string lane, Origin origin, Func<NewEvent> work)
    {
        _running[key] = new Running(key, persona, horizon, lane);
        _lanes.Post(lane, () =>
        {
            try
            {
                _outputs.Writer.TryWrite(new Output(key, persona, horizon, origin, [work()], null));
            }
            catch (Exception ex)
            {
                _outputs.Writer.TryWrite(new Output(key, persona, horizon, origin, [], ex.ToString()));
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>Decider を実行し、出力をイベントにする。</summary>
    private NewEvent Decide(StartDecider decide, SimState snapshot)
    {
        var persona = snapshot.Personas[decide.Persona];
        var seed = snapshot.Worlds[persona.World].Seed;
        var random = _random.Open(seed, persona.Id.Value, "decide", persona.Rng.GetValueOrDefault("decide"));
        var context = new DecisionContext(snapshot, decide.Persona, decide.Now, decide.Reason, decide.BlockId, _ctx, random);
        var output = _decider.Decide(context);
        return new NewEvent(decide.Now, EventTypes.ActivityChosen, persona.World, persona.Id, output.Payload, output.Guard, decide.BatchKey);
    }

    /// <summary>Planner の代わりの出力を作り、イベントにする。</summary>
    private NewEvent Plan(StartPlanner plan, SimState snapshot)
    {
        var persona = snapshot.Personas[plan.Persona];
        var planned = FallbackPlanner.Plan(_ctx.Defs, persona, plan.Now, persona.PlanVersion + 1, _ctx.Holidays);
        var guard = new Guard([new PlanVersionCheck(persona.PlanVersion)], RejectAction.Drop);
        return new NewEvent(plan.Now, EventTypes.PlanCommitted, persona.World, persona.Id, new PlanCommittedPayload(planned), guard, plan.BatchKey);
    }

    /// <summary>終わった仕事の出力を、ぜんぶ受け取る。</summary>
    private void DrainOutputs()
    {
        while (_outputs.Reader.TryRead(out var output))
        {
            Receive(output);
        }
    }

    /// <summary>仕事の出力を受け取り、積む時を待つ列に入れる。仕事の例外は外へ投げる。</summary>
    private void Receive(Output output)
    {
        _running.Remove(output.BatchKey);
        if (_dropped.Remove(output.BatchKey))
        {
            return;
        }

        if (output.Error is { } error)
        {
            throw new InvalidOperationException($"決める側の仕事が失敗した（決めどき {output.BatchKey}）: {error}");
        }

        _held.Add(output);
    }

    /// <summary>ホライズンが limit 以下になった出力を、契機の連番の順にキューへ入れる。</summary>
    private void InsertHeld(DateTimeOffset limit)
    {
        var ready = _held.Where(output => output.Horizon <= limit).OrderBy(output => output.BatchKey).ToList();
        foreach (var output in ready)
        {
            _held.Remove(output);
            foreach (var ev in output.Events)
            {
                Enqueue(ev, output.Origin, output.BatchKey);
            }
        }
    }

    /// <summary>決める側の仕事が動いている（走っている、または出力を待っている）ペルソナ。</summary>
    private ImmutableHashSet<PersonaId> Busy() =>
        _running.Values.Select(run => run.Persona).Concat(_held.Select(output => output.Persona)).ToImmutableHashSet();

    /// <summary>未適用のイベントを、ペルソナごとに引けるようにしたもの。</summary>
    private ILookup<PersonaId, SimEvent> PendingEvents() =>
        _queue.UnorderedItems
            .Where(item => item.Element.PersonaId is not null)
            .ToLookup(item => item.Element.PersonaId!.Value, item => item.Element);

    /// <summary>キューの先頭の時刻を返す。空なら null。</summary>
    private DateTimeOffset? EarliestPending() => _queue.TryPeek(out var head, out _) ? head.Time : null;

    /// <summary>実行中の決める仕事。</summary>
    private sealed record Running(long Key, PersonaId Persona, DateTimeOffset Horizon, string Lane);

    /// <summary>決める仕事の結果。</summary>
    private sealed record Output(long BatchKey, PersonaId Persona, DateTimeOffset Horizon, Origin Origin, ImmutableArray<NewEvent> Events, string? Error);
}
