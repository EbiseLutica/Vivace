using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Vivace.Core.Decision;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Planning;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;
using Vivace.Core.Signals;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>シミュレーションのエンジン。状態を書く唯一の場所で、イベントを時刻、段、連番の順に適用し、決める側の出力を取り込む。</summary>
public sealed class SimEngine : IAsyncDisposable
{
    /// <summary>購読ごとに、読まれずに溜めておける件数。超えた購読は切断する。</summary>
    public const int SubscriberCapacity = 1024;

    private readonly RuleContext _ctx;
    /// <summary>イベントの適用のエントリポイント。</summary>
    private readonly EventApplier _applier;
    /// <summary>投入の窓口の検査。</summary>
    private readonly InjectionValidator _validator;
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
    /// <summary>実行中に届いた投入のコマンド。ループの各回の先頭で取り出す。</summary>
    private readonly Channel<InjectCommand> _commands = Channel.CreateUnbounded<InjectCommand>();
    /// <summary>実行中かどうかのフラグと、投入のコマンドの受け付けを守るロック。</summary>
    private readonly Lock _gate = new();
    private readonly List<LoggedEvent> _log = [];
    private long _nextSeq;
    private long _applyNo;
    private int _sameInstantStreak;
    private SimState _state;
    /// <summary>RunUntilAsync の中にいるか。ロックを取って読み書きする。</summary>
    private bool _looping;
    /// <summary>決める側の仕事の出力が届くのを待つ仕事。キャンセルを持たせず、ループが待つ場面で使い回す。</summary>
    private Task<bool>? _outputWait;
    /// <summary>投入のコマンドが届くのを待つ仕事。キャンセルを持たせず、ループが待つ場面で使い回す。</summary>
    private Task<bool>? _commandWait;
    /// <summary>購読の一覧を守るロック。</summary>
    private readonly Lock _subscribersGate = new();
    /// <summary>購読ごとの、適用したイベントの受け渡しの列。</summary>
    private readonly List<Channel<LoggedEvent>> _subscribers = [];
    /// <summary>破棄したか。購読の一覧のロックを取って読み書きする。</summary>
    private bool _disposed;

    /// <summary>初期の状態と部品から、エンジンを作る。</summary>
    private SimEngine(RuleContext ctx, IDecider decider, IStorage storage, IRandomSource random, IClock clock)
    {
        _ctx = ctx;
        _applier = new EventApplier(ctx);
        _validator = new InjectionValidator(ctx);
        _decider = decider;
        _storage = storage;
        _random = random;
        _clock = clock;
        _state = new SimState(
            new ApplyCursor(clock.UtcNow, EventStage.DayTurn, 0, 0),
            ImmutableDictionary<WorldId, WorldState>.Empty,
            ImmutableDictionary<PersonaId, PersonaState>.Empty);
    }

    /// <summary>エンジンを作る。</summary>
    /// <param name="definitions">読み込んだ定義。</param>
    /// <param name="storage">保存先。</param>
    /// <param name="random">乱数の列を開く実装。</param>
    /// <param name="clock">時計。イベントの時刻まで待ってから適用する。</param>
    /// <param name="decider">自由時間の決定。省略すると UtilityDecider。</param>
    /// <param name="holidays">祝日のカレンダー。省略すると祝日のないカレンダー。</param>
    public static SimEngine Create(DefinitionPack definitions, IStorage storage, IRandomSource random, IClock clock, IDecider? decider = null, IHolidayCalendar? holidays = null) =>
        new(new RuleContext(definitions, RouteCache.Create(definitions), holidays ?? NoHolidays.Instance), decider ?? new UtilityDecider(), storage, random, clock);

    /// <summary>いまの状態（不変）。</summary>
    public SimState State => _state;

    /// <summary>いまの状態（不変）。読み取り用のビューで、State と同じものを返す。</summary>
    public SimState View => _state;

    /// <summary>適用の記録（適用された順）。</summary>
    public IReadOnlyList<LoggedEvent> Log => _log;

    /// <summary>
    /// 投入の窓口。要求を検査し、通ればキューに入れて連番を、通らなければ拒否の理由を返す。
    /// 実行中でなければその場で処理し、実行中は、ループが次の回の先頭で処理する。
    /// 検査は、処理する時点の状態とキューと時計の時刻で行う。
    /// </summary>
    /// <param name="request">投入の要求。</param>
    /// <param name="ct">結果を待つのをやめるキャンセル。受け付けたコマンドは取り消さない。</param>
    public Task<InjectResult> InjectAsync(InjectRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_looping)
            {
                return Task.FromResult(Inject(request));
            }

            var command = new InjectCommand(request, new TaskCompletionSource<InjectResult>(TaskCreationOptions.RunContinuationsAsynchronously));
            _commands.Writer.TryWrite(command);
            return command.Completion.Task.WaitAsync(ct);
        }
    }

    /// <summary>
    /// 適用したイベントを、保存先へのコミットのあとに、適用した順に流す。登録は呼んだ時点で行う。
    /// 読むのが遅れて、溜まった件数が容量を超えた購読は、InvalidOperationException で終わらせて切断する（エンジンは止めない）。
    /// エンジンを破棄すると、購読は正常に終わる。破棄したあとの購読は、何も流さずにすぐ終わる。
    /// </summary>
    /// <param name="ct">購読をやめるキャンセル。</param>
    public IAsyncEnumerable<LoggedEvent> Subscribe(CancellationToken ct) => Subscribe(SubscriberCapacity, ct);

    /// <summary>容量を指定して購読する。</summary>
    /// <param name="capacity">読まれずに溜めておける件数。</param>
    /// <param name="ct">購読をやめるキャンセル。</param>
    internal IAsyncEnumerable<LoggedEvent> Subscribe(int capacity, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<LoggedEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        lock (_subscribersGate)
        {
            if (_disposed)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                _subscribers.Add(channel);
            }
        }

        return ReadSubscriptionAsync(channel, ct);
    }

    /// <summary>
    /// 時刻 until まで、イベントを適用し、決める側の仕事の結果を取り込む。until より後のイベントは、キューに残す。
    /// </summary>
    /// <param name="until">進める先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task RunUntilAsync(DateTimeOffset until, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_looping)
            {
                throw new InvalidOperationException("RunUntilAsync はすでに実行中です。");
            }

            _looping = true;
        }

        try
        {
            await LoopAsync(until, ct);
        }
        finally
        {
            lock (_gate)
            {
                DrainCommands();
                _looping = false;
            }
        }
    }

    /// <summary>RunUntilAsync の本体。ループの各回の先頭で、届いたコマンドと仕事の出力を取り込む。</summary>
    private async Task LoopAsync(DateTimeOffset until, CancellationToken ct)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => cancelled.TrySetResult());
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfLaneFailed();
            DrainCommands();
            DrainOutputs();
            var head = _queue.TryPeek(out var next, out _) ? next : null;
            var limit = head?.Time ?? DateTimeOffset.MaxValue;
            if (_running.Values.Any(run => run.Horizon <= limit))
            {
                _outputWait ??= _outputs.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
                _commandWait ??= _commands.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
                await Task.WhenAny(_outputWait, _commandWait, _lanes.Failure, cancelled.Task);
                if (_outputWait.IsCompleted)
                {
                    _outputWait = null;
                }

                if (_commandWait.IsCompleted)
                {
                    _commandWait = null;
                }

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

    /// <summary>
    /// レーンを止め、動いている仕事の終わりを待ってから、保存先を閉じる。仕事の結果は捨てる。
    /// レーンの仕事の例外は、ここでは投げない（実行している間に RunUntilAsync が投げる）。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _lanes.CompleteAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await _storage.DisposeAsync();
        lock (_subscribersGate)
        {
            _disposed = true;
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }

    /// <summary>決める側の仕事の実行の列。</summary>
    internal Lanes Lanes => _lanes;

    /// <summary>購読の列を読み、やめたら購読の一覧から外す。</summary>
    private async IAsyncEnumerable<LoggedEvent> ReadSubscriptionAsync(Channel<LoggedEvent> channel, [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var logged in channel.Reader.ReadAllAsync(ct))
            {
                yield return logged;
            }
        }
        finally
        {
            lock (_subscribersGate)
            {
                _subscribers.Remove(channel);
            }

            channel.Writer.TryComplete();
        }
    }

    /// <summary>適用したイベントを、すべての購読に流す。容量を超えた購読は、例外で終わらせて外す。</summary>
    private void Publish(LoggedEvent logged)
    {
        lock (_subscribersGate)
        {
            for (var i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (!_subscribers[i].Writer.TryWrite(logged))
                {
                    _subscribers[i].Writer.TryComplete(new InvalidOperationException("購読が追いつかず、溜まった件数が容量を超えたので切断しました。"));
                    _subscribers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>要求を検査し、通ればキューに入れる。</summary>
    private InjectResult Inject(InjectRequest request)
    {
        var pending = _queue.UnorderedItems.Select(item => item.Element);
        if (!_validator.TryValidate(_state, pending, request, _clock.UtcNow, out var draft, out var reason))
        {
            return InjectResult.Reject(reason);
        }

        return InjectResult.Accept(Enqueue(draft, request.Origin, null));
    }

    /// <summary>届いている投入のコマンドを、届いた順にすべて処理する。</summary>
    private void DrainCommands()
    {
        while (_commands.Reader.TryRead(out var command))
        {
            command.Completion.TrySetResult(Inject(command.Request));
        }
    }

    /// <summary>レーンの仕事が例外を投げていたら、エンジンを止めるために例外を投げる。</summary>
    private void ThrowIfLaneFailed()
    {
        if (_lanes.Failure.IsCompleted)
        {
            throw new InvalidOperationException("決める側のレーンの仕事が失敗しました。", _lanes.Failure.Result);
        }
    }

    /// <summary>1つのイベントを適用する。不変条件を検査してからコミットし、シグナルを実行する。</summary>
    private async Task ApplyAsync(SimEvent ev, CancellationToken ct)
    {
        var previous = _state.Cursor.Time;
        await _clock.WaitUntilAsync(ev.Time, ct);
        var result = _applier.Apply(_state, ev);
        long? applyNo = null;
        if (result.Status == EventStatus.Applied)
        {
            applyNo = ++_applyNo;
            _state = result.State with { Cursor = new ApplyCursor(ev.Time, ev.Stage, ev.Seq, applyNo.Value) };
        }
        else
        {
            _state = result.State;
        }

        var logged = new LoggedEvent(ev, result.Status, applyNo, result.Reason);
        _log.Add(logged);
        foreach (var followUp in result.FollowUps)
        {
            Enqueue(followUp, ev.Origin, ev.Seq);
        }

        if (result.Status == EventStatus.Applied)
        {
            _sameInstantStreak = ev.Time == previous ? _sameInstantStreak + 1 : 0;
            Invariants.CheckProgress(_sameInstantStreak);
            Invariants.Check(_ctx, _state, previous, BusyAfter(result.Signals), PendingEvents(), EarliestPending());
        }

        await _storage.CommitAsync(new StorageBatch([logged], _state), ct);
        if (logged.Status == EventStatus.Applied)
        {
            Publish(logged);
        }

        foreach (var signal in result.Signals)
        {
            Handle(signal, ev.Origin);
        }
    }

    /// <summary>適用の後に動いている決める仕事のペルソナ。これから起動するシグナルの分も含める。</summary>
    private ImmutableHashSet<PersonaId> BusyAfter(ImmutableArray<EngineSignal> signals) =>
        Busy().Union(signals.Select(signal => signal switch
        {
            StartDeciderSignal decide => decide.Persona,
            StartPlannerSignal plan => plan.Persona,
            _ => (PersonaId?)null,
        }).Where(persona => persona is not null).Select(persona => persona!.Value));

    /// <summary>後続のイベントに連番と発生元を付けて、キューに入れる。</summary>
    private long Enqueue(EventDraft ev, Origin origin, long? cause)
    {
        var seq = ++_nextSeq;
        var stage = EventTypes.StageOf(ev.EventType);
        var simEvent = new SimEvent(
            seq,
            ev.Time,
            ev.EventType,
            ev.WorldId,
            ev.PersonaId,
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
            case StartDeciderSignal decide:
                var decideSnapshot = _state;
                StartHandler(decide.BatchSeq, decide.Persona, decide.Now, $"decide:{decide.Persona}", origin, () => Decide(decide, decideSnapshot));
                break;
            case StartPlannerSignal plan:
                var planSnapshot = _state;
                StartHandler(plan.BatchSeq, plan.Persona, plan.Now, $"plan:{plan.Persona}", origin, () => Plan(plan, planSnapshot));
                break;
            case CancelHandlerSignal cancel:
                foreach (var run in _running.Values.Where(run => run.Persona == cancel.Persona && IsCancellable(run.Lane)).ToList())
                {
                    _running.Remove(run.Key);
                    _dropped.Add(run.Key);
                }

                break;
        }
    }

    /// <summary>取り消しの対象になるレーンか。決める側のレーンだけを止め、Planner は止めない。</summary>
    /// <param name="lane">レーンのキー。</param>
    internal static bool IsCancellable(string lane) => lane.StartsWith("decide:", StringComparison.Ordinal);

    /// <summary>決める側の仕事を、レーンで起動する。仕事はスナップショット（起動の時点の状態）だけを読む。</summary>
    private void StartHandler(long key, PersonaId persona, DateTimeOffset horizon, string lane, Origin origin, Func<EventDraft> work)
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
    private EventDraft Decide(StartDeciderSignal decide, SimState snapshot)
    {
        var persona = snapshot.Personas[decide.Persona];
        var seed = snapshot.Worlds[persona.World].Seed;
        var random = _random.Open(seed, persona.Id.Value, "decide", persona.Rng.GetValueOrDefault("decide"));
        var context = new DecisionContext(snapshot, decide.Persona, decide.Now, decide.Reason, decide.BlockId, _ctx, random);
        var output = _decider.Decide(context);
        return new EventDraft(decide.Now, EventTypes.ActivityChosen, persona.World, persona.Id, output.Payload, output.Guard, decide.BatchSeq);
    }

    /// <summary>Planner の代わりの出力を作り、イベントにする。</summary>
    private EventDraft Plan(StartPlannerSignal plan, SimState snapshot)
    {
        var persona = snapshot.Personas[plan.Persona];
        var planned = FallbackPlanner.Plan(_ctx.Defs, persona, plan.Now, persona.PlanVersion + 1, _ctx.Holidays);
        var guard = new Guard([new PlanVersionCheck(persona.PlanVersion)], RejectAction.Drop);
        return new EventDraft(plan.Now, EventTypes.PlanCommitted, persona.World, persona.Id, new PlanCommittedPayload(planned), guard, plan.BatchSeq);
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
        _running.Remove(output.BatchSeq);
        if (_dropped.Remove(output.BatchSeq))
        {
            return;
        }

        if (output.Error is { } error)
        {
            throw new InvalidOperationException($"決める側の仕事が失敗しました（決めどき {output.BatchSeq}）: {error}");
        }

        _held.Add(output);
    }

    /// <summary>ホライズンが limit 以下になった出力を、契機の連番の順にキューへ入れる。</summary>
    private void InsertHeld(DateTimeOffset limit)
    {
        var ready = _held.Where(output => output.Horizon <= limit).OrderBy(output => output.BatchSeq).ToList();
        foreach (var output in ready)
        {
            _held.Remove(output);
            foreach (var ev in output.Events)
            {
                Enqueue(ev, output.Origin, output.BatchSeq);
            }
        }
    }

    /// <summary>決める側の仕事が動いている（実行中、または出力を待っている）ペルソナ。</summary>
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

    /// <summary>実行中に届いた投入のコマンド。</summary>
    private sealed record InjectCommand(InjectRequest Request, TaskCompletionSource<InjectResult> Completion);

    /// <summary>決める仕事の結果。</summary>
    private sealed record Output(long BatchSeq, PersonaId Persona, DateTimeOffset Horizon, Origin Origin, ImmutableArray<EventDraft> Events, string? Error);
}
