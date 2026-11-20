using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Vivace.Core.Channels;
using Vivace.Core.Decision;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Handlers;
using Vivace.Core.Llm;
using Vivace.Core.Planning;
using Vivace.Core.Randomness;
using Vivace.Core.Recovery;
using Vivace.Core.Routes;
using Vivace.Core.Signals;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>シミュレーションのエンジン。状態を書く唯一の場所で、イベントを時刻、ステージ、連番の順に適用し、決める側の出力を取り込む。</summary>
public sealed class SimEngine : IAsyncDisposable
{
    /// <summary>購読ごとに、読まれずに溜めておける件数。超えた購読は切断する。</summary>
    public const int SubscriberCapacity = 1024;

    private readonly RuleContext _ctx;
    /// <summary>イベントの適用のエントリポイント。</summary>
    private readonly EventApplier _applier;
    /// <summary>投入 API の検査。</summary>
    private readonly InjectionValidator _validator;
    private readonly IDecider _decider;
    private readonly IStorage _storage;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    /// <summary>LLM。効果のハンドラが使う。</summary>
    private readonly ILlmClient _llm;
    private readonly Lanes _lanes = new();
    /// <summary>配信役。ドライランでは null にして、送らない。</summary>
    private readonly Dispatcher? _dispatcher;
    /// <summary>エンジンを止めるキャンセル。破棄のときに取り消し、送信中の配信に伝える。</summary>
    private readonly CancellationTokenSource _shutdown = new();
    /// <summary>配信役に渡して、まだ結果が戻っていない操作の数。エンジンの処理のスレッドだけが書く。</summary>
    private int _deliveriesInFlight;
    /// <summary>エンジンの処理（RunUntilAsync と、実行していないときの投入）を1つずつにする。</summary>
    private readonly SemaphoreSlim _engineGate = new(1, 1);
    /// <summary>未適用のイベント。時刻、ステージ、連番の順に取り出す。</summary>
    private readonly PriorityQueue<SimEvent, (DateTimeOffset Time, int Stage, long Seq)> _queue = new();
    private readonly Dictionary<long, Running> _running = [];
    private readonly List<Output> _held = [];
    private readonly HashSet<long> _dropped = [];
    /// <summary>起動したジョブの取り消し元。エンジンの処理のスレッドだけが読み書きし、出力を受け取ったときに破棄する。破棄のときに残りも破棄する。</summary>
    private readonly Dictionary<long, CancellationTokenSource> _handlerCancels = [];
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
    /// <summary>決める側のジョブの出力が届くのを待つタスク。キャンセルを持たせず、ループが待つ場面で使い回す。</summary>
    private Task<bool>? _outputWait;
    /// <summary>投入のコマンドが届くのを待つタスク。キャンセルを持たせず、ループが待つ場面で使い回す。</summary>
    private Task<bool>? _commandWait;
    /// <summary>購読の一覧を守るロック。</summary>
    private readonly Lock _subscribersGate = new();
    /// <summary>購読ごとの、適用したイベントの受け渡しの列。</summary>
    private readonly List<Channel<LoggedEvent>> _subscribers = [];
    /// <summary>破棄したか。購読の一覧のロックを取って読み書きする。</summary>
    private bool _disposed;

    /// <summary>初期の状態と部品から、エンジンを作る。</summary>
    private SimEngine(RuleContext ctx, IDecider decider, IStorage storage, IRandomSource random, IClock clock, ILlmClient llm, Dispatcher? dispatcher)
    {
        _ctx = ctx;
        _llm = llm;
        _dispatcher = dispatcher;
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
    /// <param name="llm">LLM。省略すると、呼ぶと失敗する実装（投稿はスキップになる）。</param>
    /// <param name="channels">送信に使うチャンネル。省略すると、どのチャンネルもない（送信はすべて失敗する）。</param>
    /// <param name="dispatch">送信するかどうか。false なら配信役を置かず、未送信の操作は残す（ドライラン）。</param>
    /// <param name="sendTimeout">送信の時間切れ（実時間）。省略すると既定の 30 秒。</param>
    public static SimEngine Create(
        DefinitionPack definitions,
        IStorage storage,
        IRandomSource random,
        IClock clock,
        IDecider? decider = null,
        IHolidayCalendar? holidays = null,
        ILlmClient? llm = null,
        IReadOnlyList<IChannel>? channels = null,
        bool dispatch = true,
        TimeSpan? sendTimeout = null) =>
        new(
            new RuleContext(definitions, RouteCache.Create(definitions), holidays ?? NoHolidays.Instance),
            decider ?? new UtilityDecider(),
            storage,
            random,
            clock,
            llm ?? new NoLlmClient(),
            dispatch ? new Dispatcher(channels ?? [], sendTimeout ?? Dispatcher.DefaultSendTimeout) : null);

    /// <summary>いまの状態（不変）。</summary>
    public SimState State => _state;

    /// <summary>いまの状態（不変）。読み取り用のビューで、State と同じものを返す。</summary>
    public SimState View => _state;

    /// <summary>適用の記録（適用された順）。</summary>
    public IReadOnlyList<LoggedEvent> Log => _log;

    /// <summary>
    /// 投入 API。要求を検査し、通ればキューに入れて（保存先にも書いて）連番を、通らなければ拒否の理由を返す。
    /// 実行中でなければその場で処理し、実行中は、ループが次の回の先頭で処理する。
    /// 検査は、処理する時点の状態とキューと時計の時刻で行う。
    /// </summary>
    /// <param name="request">投入の要求。</param>
    /// <param name="ct">結果を待つのをやめるキャンセル。受け付けたコマンドは取り消さない。</param>
    public async Task<InjectResult> InjectAsync(InjectRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<InjectResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Writer.TryWrite(new InjectCommand(request, completion));
        await PumpCommandsAsync();
        return await completion.Task.WaitAsync(ct);
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
    /// 保存先の記録から状態とキューを戻し、処理中のイベントのハンドラを起動し直す。起動の直後に一度だけ呼ぶ。記録がなければ何もしない。
    /// 処理中のハンドラの出力は、取り込むまでは保存先に残るので、起動し直した出力で改めて適用される。
    /// </summary>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="InvalidOperationException">すでに投入したか適用したあとに呼んだ。</exception>
    public async Task RecoverAsync(CancellationToken ct)
    {
        await _engineGate.WaitAsync(ct);
        try
        {
            if (_nextSeq != 0 || _applyNo != 0)
            {
                throw new InvalidOperationException("RecoverAsync は、起動の直後に一度だけ呼びます。");
            }

            var recovered = await _storage.LoadAsync(ct);
            if (recovered.State is { } state)
            {
                _state = state;
                _applyNo = state.Cursor.ApplyNo;
            }

            _nextSeq = recovered.MaxSeq;
            foreach (var row in recovered.Open.Where(row => row.Status == EventRowStatus.Pending).OrderBy(row => row.Event.Seq))
            {
                _queue.Enqueue(row.Event, (row.Event.Time, (int)row.Event.Stage, row.Event.Seq));
            }

            foreach (var row in recovered.Open.Where(row => row.Status == EventRowStatus.Processing).OrderBy(row => row.Event.Seq))
            {
                Handle(ProcessingSignal.For(_ctx, row.Event), row.Event.Origin);
            }
        }
        finally
        {
            _engineGate.Release();
        }
    }

    /// <summary>
    /// 時刻 until まで、イベントを適用し、決める側のジョブの結果を取り込む。until より後のイベントは、キューに残す。
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

        await _engineGate.WaitAsync(CancellationToken.None);
        try
        {
            await LoopAsync(until, ct);
            await DrainCommandsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            FailCommands(ex);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                _looping = false;
            }

            _engineGate.Release();
        }

        await PumpCommandsAsync();
    }

    /// <summary>
    /// 届いている投入のコマンドを、エンジンが止まっていれば、この場で処理する。エンジンが回っていれば、ループが処理するので何もしない。
    /// 処理のあと、ゲートを離してから届いたものも、取り残さないように読み直す。
    /// </summary>
    private async Task PumpCommandsAsync()
    {
        while (_commands.Reader.TryPeek(out _) && await _engineGate.WaitAsync(0))
        {
            try
            {
                await DrainCommandsAsync(CancellationToken.None);
            }
            finally
            {
                _engineGate.Release();
            }
        }
    }

    /// <summary>RunUntilAsync の本体。ループの各回の先頭で、届いたコマンドとジョブの出力を取り込む。</summary>
    private async Task LoopAsync(DateTimeOffset until, CancellationToken ct)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => cancelled.TrySetResult());
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfLaneFailed();
            await DrainCommandsAsync(ct);
            await DrainOutputsAsync(ct);
            if (_deliveriesInFlight > 0)
            {
                _commandWait ??= _commands.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
                await Task.WhenAny(_commandWait, _lanes.Failure, cancelled.Task);
                if (_commandWait.IsCompleted)
                {
                    _commandWait = null;
                }

                continue;
            }

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
                await InsertHeldAsync(limit, ct);
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
    /// レーンを止め、動いているジョブの終わりを待ってから、保存先を閉じる。ジョブの結果は捨てる。
    /// レーンのジョブの例外は、ここでは投げない（実行している間に RunUntilAsync が投げる）。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        await _lanes.CompleteAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        foreach (var handlerCancel in _handlerCancels.Values)
        {
            handlerCancel.Dispose();
        }

        _handlerCancels.Clear();
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

    /// <summary>決める側のジョブキュー。</summary>
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

    /// <summary>要求を検査し、通ればキューに入れ、積んだものを保存先に書く。</summary>
    private async Task<InjectResult> InjectNowAsync(InjectRequest request, CancellationToken ct)
    {
        var added = new List<SimEvent>();
        var result = Inject(request, added);
        if (added.Count > 0)
        {
            await _storage.CommitAsync(new StorageBatch(added.ToImmutableArray(), ImmutableArray<EventRowUpdate>.Empty, _state), ct);
        }

        return result;
    }

    /// <summary>要求を検査し、通ればキューに入れる。積んだイベントは added に足す。</summary>
    private InjectResult Inject(InjectRequest request, List<SimEvent> added)
    {
        var pending = _queue.UnorderedItems.Select(item => item.Element);
        if (!_validator.TryValidate(_state, pending, request, _clock.UtcNow, out var draft, out var reason))
        {
            return InjectResult.Reject(reason);
        }

        return InjectResult.Accept(Enqueue(draft, request.Origin, null, added));
    }

    /// <summary>届いている投入のコマンドを、届いた順にすべて処理する。</summary>
    private async Task DrainCommandsAsync(CancellationToken ct)
    {
        while (_commands.Reader.TryRead(out var command))
        {
            if (command.Delivery)
            {
                _deliveriesInFlight--;
            }

            InjectResult result;
            try
            {
                result = await InjectNowAsync(command.Request, ct);
            }
            catch (Exception ex)
            {
                command.Completion?.TrySetException(ex);
                FailCommands(ex);
                throw;
            }

            command.Completion?.TrySetResult(result);
        }
    }

    /// <summary>届いている投入のコマンドを、すべて例外で失敗させる。適用されなかったものは、待っている呼び出し側に知らせる。</summary>
    /// <param name="ex">失敗の原因。</param>
    private void FailCommands(Exception ex)
    {
        while (_commands.Reader.TryRead(out var command))
        {
            command.Completion?.TrySetException(ex);
        }
    }

    /// <summary>レーンのジョブが例外を投げていたら、エンジンを止めるために例外を投げる。</summary>
    private void ThrowIfLaneFailed()
    {
        if (_lanes.Failure.IsCompleted)
        {
            throw new InvalidOperationException("決める側のレーンのジョブが失敗しました。", _lanes.Failure.Result);
        }
    }

    /// <summary>
    /// 1つのイベントを適用する。不変条件を検査し、後続のイベントと行の状態を1つのコミットで書いてから、シグナルを実行する。
    /// シグナルを出したイベントは、出力を取り込むまで処理中の行にする。
    /// </summary>
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
        var added = new List<SimEvent>();
        foreach (var followUp in result.FollowUps)
        {
            Enqueue(followUp, ev.Origin, ev.Seq, added);
        }

        if (result.Status == EventStatus.Applied)
        {
            _sameInstantStreak = ev.Time == previous ? _sameInstantStreak + 1 : 0;
            Invariants.CheckProgress(_sameInstantStreak);
            Invariants.Check(_ctx, _state, previous, BusyAfter(result.Signals), PendingEvents(), EarliestPending());
        }

        var update = new EventRowUpdate(ev.Seq, RowStatusOf(result), applyNo, result.Reason);
        await _storage.CommitAsync(new StorageBatch(added.ToImmutableArray(), ImmutableArray.Create(update), _state), ct);
        if (logged.Status == EventStatus.Applied)
        {
            Publish(logged);
        }

        foreach (var signal in result.Signals)
        {
            Handle(signal, ev.Origin);
        }
    }

    /// <summary>適用の結果を行の状態にする。決める側のハンドラを起こすイベントは、出力を取り込むまで処理中にする。</summary>
    /// <param name="result">適用の結果。</param>
    private static EventRowStatus RowStatusOf(ApplyResult result) => result.Status switch
    {
        EventStatus.Applied => result.Signals.OfType<StartHandlerSignal>().Any() ? EventRowStatus.Processing : EventRowStatus.Applied,
        EventStatus.Skipped => EventRowStatus.Skipped,
        _ => EventRowStatus.Rejected,
    };

    /// <summary>適用の後に動いている決めるジョブのペルソナ。これから起動するシグナルの分も含める。</summary>
    private ImmutableHashSet<PersonaId> BusyAfter(ImmutableArray<EngineSignal> signals) =>
        Busy().Union(signals.OfType<StartHandlerSignal>().Select(start => start.Persona));

    /// <summary>後続のイベントに連番と発生元を付けて、キューに入れ、積んだ一覧に足す。</summary>
    private long Enqueue(EventDraft ev, Origin origin, long? cause, List<SimEvent> added)
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
        added.Add(simEvent);
        return seq;
    }

    /// <summary>シグナルを実行する。</summary>
    private void Handle(EngineSignal signal, Origin origin)
    {
        switch (signal)
        {
            case StartDeciderSignal decide:
                var decideSnapshot = _state;
                StartHandler(decide, $"decide:{decide.Persona}", origin, _ => Task.FromResult(Decide(decide, decideSnapshot)), () => FallbackDraft(decide, decideSnapshot));
                break;
            case StartPlannerSignal plan:
                var planSnapshot = _state;
                StartHandler(plan, $"plan:{plan.Persona}", origin, _ => Task.FromResult(Plan(plan, planSnapshot)), null);
                break;
            case StartEffectSignal effect:
                var effectSnapshot = _state;
                StartHandler(effect, $"decide:{effect.Persona}", origin, ct => SnsPostHandler.RunAsync(_ctx, _llm, effectSnapshot, effect, ct), () => SnsPostHandler.Skipped(effectSnapshot, effect));
                break;
            case DeliverSignal deliver:
                Deliver(deliver);
                break;
            case CancelHandlerSignal cancel:
                foreach (var run in _running.Values.Where(run => run.Persona == cancel.Persona && IsCancellable(run.Lane)).ToList())
                {
                    _running.Remove(run.Key);
                    _dropped.Add(run.Key);
                    if (_handlerCancels.TryGetValue(run.Key, out var handlerCancel))
                    {
                        handlerCancel.Cancel();
                    }
                }

                break;
        }
    }

    /// <summary>
    /// 未送信の操作を配信役に渡す。送信は別のタスクで行い、結果は配信の結果のコマンドとして戻す。配信役がなければ何もしない。
    /// </summary>
    /// <param name="signal">配信のシグナル。</param>
    private void Deliver(DeliverSignal signal)
    {
        if (_dispatcher is null)
        {
            return;
        }

        var operation = signal.Operation;
        var request = new OutboundRequest(operation.Seq, signal.Persona, _clock.UtcNow, operation.OperationKind, operation.IsSpeech, operation.Payload);
        _deliveriesInFlight++;
        _ = Task.Run(() => DeliverAsync(_dispatcher, operation, request, _shutdown.Token));
    }

    /// <summary>
    /// 配信役に送らせ、結果を配信の結果のコマンドとして戻す。どの経路でも結果を1つ戻す（例外は失敗の結果にする）。
    /// </summary>
    /// <param name="dispatcher">配信役。</param>
    /// <param name="operation">送る未送信の操作。</param>
    /// <param name="request">チャンネルに渡す操作。</param>
    /// <param name="ct">エンジンを止めるキャンセル。</param>
    private async Task DeliverAsync(Dispatcher dispatcher, OutboundOp operation, OutboundRequest request, CancellationToken ct)
    {
        InjectRequest injection;
        try
        {
            var result = await dispatcher.SendAsync(operation.Channel, request, ct);
            injection = DeliveryRequest(operation, result);
        }
        catch (Exception ex)
        {
            injection = DeliveryRequest(operation, new SendResult.Failed(ex.Message, SurelyNotSent: false, RetryAfter: null));
        }

        _commands.Writer.TryWrite(new InjectCommand(injection, null, Delivery: true));
    }

    /// <summary>送信の結果を、送信の完了か失敗のイベントの投入にする。</summary>
    /// <param name="operation">送った未送信の操作。</param>
    /// <param name="result">送信の結果。</param>
    private static InjectRequest DeliveryRequest(OutboundOp operation, SendResult result)
    {
        var origin = Origin.FromChannel(operation.Channel);
        return result switch
        {
            SendResult.Sent sent => new InjectRequest(EventTypes.OutboundSent, new OutboundSentPayload(operation.Seq, sent.ExternalId), null, origin),
            SendResult.Failed failed => new InjectRequest(EventTypes.OutboundFailed, new OutboundFailedPayload(operation.Seq, failed.Reason, failed.RetryAfter), null, origin),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
        };
    }

    /// <summary>取り消しの対象になるレーンか。決める側のレーンだけを止め、Planner は止めない。</summary>
    /// <param name="lane">レーンのキー。</param>
    internal static bool IsCancellable(string lane) => lane.StartsWith("decide:", StringComparison.Ordinal);

    /// <summary>
    /// 決める側のジョブを、レーンで起動する。ジョブはスナップショット（起動の時点の状態）だけを読む。
    /// fallback があれば、ジョブの例外は代わりの出力にする。なければ、例外はレーンを閉じ、エンジンを止める。
    /// </summary>
    private void StartHandler(StartHandlerSignal start, string lane, Origin origin, Func<CancellationToken, Task<EventDraft>> work, Func<EventDraft>? fallback)
    {
        _running[start.BatchSeq] = new Running(start.BatchSeq, start.Persona, start.Horizon, lane);
        var handlerCancel = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        _handlerCancels[start.BatchSeq] = handlerCancel;
        _lanes.Post(lane, async () =>
        {
            EventDraft draft;
            try
            {
                draft = await work(handlerCancel.Token);
            }
            catch (Exception) when (fallback is not null)
            {
                draft = fallback();
            }

            _outputs.Writer.TryWrite(new Output(start.BatchSeq, start.Persona, start.Horizon, origin, [draft]));
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
        return ActivityChosenDraft(decide, persona, output);
    }

    /// <summary>Decider が例外で失敗したときの代わりの出力を、イベントにする。</summary>
    private EventDraft FallbackDraft(StartDeciderSignal decide, SimState snapshot)
    {
        var persona = snapshot.Personas[decide.Persona];
        var output = decide.Reason == DecisionReason.Phone
            ? DecisionFallback.Phone(persona)
            : DecisionFallback.FreeTime(_ctx, snapshot, persona, decide.Now);
        return ActivityChosenDraft(decide, persona, output);
    }

    /// <summary>決める側の出力を、行動の決定のイベントにする。</summary>
    private static EventDraft ActivityChosenDraft(StartDeciderSignal decide, PersonaState persona, DeciderOutput output) =>
        new(decide.Now, EventTypes.ActivityChosen, persona.World, persona.Id, output.Payload, output.Guard, decide.BatchSeq);

    /// <summary>Planner の代わりの出力を作り、イベントにする。</summary>
    private EventDraft Plan(StartPlannerSignal plan, SimState snapshot)
    {
        var persona = snapshot.Personas[plan.Persona];
        var planned = FallbackPlanner.Plan(_ctx.Defs, persona, plan.Now, persona.PlanVersion + 1, _ctx.Holidays);
        var guard = new Guard([new PlanVersionCheck(persona.PlanVersion)], RejectAction.Drop);
        return new EventDraft(plan.Now, EventTypes.PlanCommitted, persona.World, persona.Id, new PlanCommittedPayload(planned), guard, plan.BatchSeq);
    }

    /// <summary>
    /// 終わったジョブの出力を、ぜんぶ受け取る。捨てたものは、処理中のトリガーを適用済みにするので、その分の更新を1つのコミットで書く。
    /// </summary>
    private async Task DrainOutputsAsync(CancellationToken ct)
    {
        var updates = new List<EventRowUpdate>();
        while (_outputs.Reader.TryRead(out var output))
        {
            if (Receive(output))
            {
                updates.Add(new EventRowUpdate(output.BatchSeq, EventRowStatus.Applied, null, null));
            }
        }

        if (updates.Count > 0)
        {
            await _storage.CommitAsync(new StorageBatch(ImmutableArray<SimEvent>.Empty, updates.ToImmutableArray(), _state), ct);
        }
    }

    /// <summary>ジョブの出力を受け取り、積む時を待つ列に入れる。取り消したものは捨て、捨てたなら true を返す。</summary>
    /// <param name="output">ジョブの出力。</param>
    private bool Receive(Output output)
    {
        _running.Remove(output.BatchSeq);
        if (_handlerCancels.Remove(output.BatchSeq, out var handlerCancel))
        {
            handlerCancel.Dispose();
        }

        if (_dropped.Remove(output.BatchSeq))
        {
            return true;
        }

        _held.Add(output);
        return false;
    }

    /// <summary>
    /// ホライズンが limit 以下になった出力を、トリガーの連番の順にキューへ入れる。積んだイベントを保存先に書き、
    /// トリガーの処理中の行を適用済みにする（1つのコミット）。
    /// </summary>
    /// <param name="limit">この時刻までのホライズンの出力を入れる。</param>
    /// <param name="ct">キャンセル。</param>
    private async Task InsertHeldAsync(DateTimeOffset limit, CancellationToken ct)
    {
        var ready = _held.Where(output => output.Horizon <= limit).OrderBy(output => output.BatchSeq).ToList();
        var added = new List<SimEvent>();
        var updates = new List<EventRowUpdate>();
        foreach (var output in ready)
        {
            _held.Remove(output);
            updates.Add(new EventRowUpdate(output.BatchSeq, EventRowStatus.Applied, null, null));
            foreach (var ev in output.Events)
            {
                Enqueue(ev, output.Origin, output.BatchSeq, added);
            }
        }

        await _storage.CommitAsync(new StorageBatch(added.ToImmutableArray(), updates.ToImmutableArray(), _state), ct);
    }

    /// <summary>決める側のジョブが動いている（実行中、または出力を待っている）ペルソナ。</summary>
    private ImmutableHashSet<PersonaId> Busy() =>
        _running.Values.Select(run => run.Persona).Concat(_held.Select(output => output.Persona)).ToImmutableHashSet();

    /// <summary>未適用のイベントを、ペルソナごとに引けるようにしたもの。</summary>
    private ILookup<PersonaId, SimEvent> PendingEvents() =>
        _queue.UnorderedItems
            .Where(item => item.Element.PersonaId is not null)
            .ToLookup(item => item.Element.PersonaId!.Value, item => item.Element);

    /// <summary>キューの先頭の時刻を返す。空なら null。</summary>
    private DateTimeOffset? EarliestPending() => _queue.TryPeek(out var head, out _) ? head.Time : null;

    /// <summary>実行中の決めるジョブ。</summary>
    private sealed record Running(long Key, PersonaId Persona, DateTimeOffset Horizon, string Lane);

    /// <summary>実行中に届いた投入のコマンド。配信の結果のときは Completion を持たず、Delivery を真にする。</summary>
    private sealed record InjectCommand(InjectRequest Request, TaskCompletionSource<InjectResult>? Completion, bool Delivery = false);

    /// <summary>決めるジョブの結果。</summary>
    private sealed record Output(long BatchSeq, PersonaId Persona, DateTimeOffset Horizon, Origin Origin, ImmutableArray<EventDraft> Events);
}
