using Vivace.Core;
using Vivace.Core.Channels;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

using Vivace.Core.Llm;
using Vivace.Core.Randomness;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;
using Vivace.Core.Time;
using Vivace.Storage.Sqlite;
using Vivace.Testing;
using Vivace.Tests.Storage;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// 止めていた間を、仮想の時計で早送りして追いつく。追いつきの間は配信せず、購読に流さず、積むイベントの発生元は catchup で、
/// 起動のたびに圏外明けを伝える。
/// </summary>
public class CatchUpTests
{
    /// <summary>投稿が出る窓で止める時刻（投稿は1日目の終わりに出る）。</summary>
    private static readonly DateTimeOffset StopAt = Scenario.Start.AddDays(1);

    /// <summary>投稿が出る窓で、いまに着く時刻。</summary>
    private static readonly DateTimeOffset Now = Scenario.Start.AddDays(2);

    /// <summary>投稿が出ない窓で止める時刻（この窓では、止めずに回したときも SNS の行動を選ばない）。</summary>
    private static readonly DateTimeOffset QuietStop = Scenario.Start.AddHours(1);

    /// <summary>投稿が出ない窓で、いまに着く時刻。</summary>
    private static readonly DateTimeOffset QuietNow = Scenario.Start.AddHours(8);

    /// <summary>
    /// 投稿の出ない窓で止めて起動し直すと、止めなかったときと事実の列が一致する。種類、状態、適用番号、連番の参照、中身を比べる。
    /// 発生元は catchup に変わるので比べない。投稿の代わりの出力（post.skipped）は、理由だけ正規化して比べる。
    /// </summary>
    [Fact]
    public async Task CatchUpGivesTheSameFactsAsAnUninterruptedRun()
    {
        var ct = TestContext.Current.CancellationToken;
        using var referenceTemp = new TempDatabase();
        var reference = await FirstRunAsync(referenceTemp.FilePath, QuietNow, ct);

        using var temp = new TempDatabase();
        var first = await FirstRunAsync(temp.FilePath, QuietStop, ct);
        var resumed = await ResumeAsync(temp.FilePath, QuietNow, null, ct);

        Assert.Equal(Facts(reference), Facts([.. first, .. resumed]));
        Assert.Contains(resumed, entry => entry.Event.Origin == Origin.Catchup);
        Assert.DoesNotContain(first, entry => entry.Event.Origin == Origin.Catchup);
    }

    /// <summary>
    /// 追いつきの間は、配信しない。追いついたあとに、チャンネルへ圏外明けを1回伝える（圏外だった区間は、記録の最後の適用から、いまに着いた時刻まで）。
    /// </summary>
    [Fact]
    public async Task CatchUpSendsNothingAndTellsChannelsOnline()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var first = await FirstRunWithPostsAsync(temp.FilePath, StopAt, ct);
        Assert.Contains(first, entry => entry.Event.EventType == EventTypes.OutboundSpeech);
        var lastApplied = LastApplied(first);

        var channel = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Now), PostingLlm(), [channel], ct);
        await engine.RecoverAsync(ct);
        Assert.Empty(channel.Sent);
        Assert.DoesNotContain(engine.Log, entry => entry.Event.Origin == Origin.Catchup && entry.Event.EventType == EventTypes.ActionStarted && ((ActionStartedPayload)entry.Event.Payload).ActionId == "post_sns");

        await engine.DisposeAsync();
        AssertOnline(channel, lastApplied, Now);
        Assert.Empty(channel.Sent);
    }

    /// <summary>
    /// 追いつきで進めたイベントは、購読に流さない（ライブの出力に、止めていた間の分を流さないため）。記録（Log）には残る。
    /// </summary>
    [Fact]
    public async Task CatchUpEventsAreNotPublishedToSubscribers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        await FirstRunAsync(temp.FilePath, StopAt, ct);

        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Now), null, [], ct);
        var feed = engine.Subscribe(ct);
        var read = Task.Run(async () =>
        {
            var seen = new List<LoggedEvent>();
            await foreach (var logged in feed)
            {
                seen.Add(logged);
            }

            return seen;
        }, ct);
        await engine.RecoverAsync(ct);
        await engine.RunUntilAsync(Now, ct);
        Assert.Contains(engine.Log, entry => entry.Event.Origin == Origin.Catchup);
        await engine.DisposeAsync();

        Assert.Empty(await read);
    }

    /// <summary>
    /// 止めていた間が 14 日を超えると、起動を止めてエラーにする。何も適用せず、エラーの文で persona.gap が未実装だと分かる。
    /// </summary>
    [Fact]
    public async Task GapOverFourteenDaysStopsStartup()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var first = await FirstRunAsync(temp.FilePath, StopAt, ct);
        var lastApplied = LastApplied(first);

        var engine = await OpenAsync(temp.FilePath, new VirtualClock(lastApplied.AddDays(14).AddSeconds(1)), null, [], ct);
        await using (engine)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecoverAsync(ct));

            Assert.Contains("persona.gap", error.Message);
            Assert.Empty(engine.Log);
        }
    }

    /// <summary>
    /// 止めていた間が 1 分以内でも、追いつきには入らず、記録から起動したので圏外明けは伝える（区間は、最後の適用から、いまの時刻まで）。
    /// </summary>
    [Fact]
    public async Task ShortGapIsNotCaughtUpButStillTellsChannelsOnline()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var first = await FirstRunAsync(temp.FilePath, StopAt, ct);
        var lastApplied = LastApplied(first);
        var now = lastApplied.AddSeconds(59);

        var channel = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(now), null, [channel], ct);
        await engine.RecoverAsync(ct);
        Assert.Empty(engine.Log);
        await engine.DisposeAsync();

        AssertOnline(channel, lastApplied, now);
    }

    /// <summary>
    /// 記録がなければ、圏外明けは伝えない（起動の記録がないので、圏外だった区間もない）。
    /// </summary>
    [Fact]
    public async Task FreshStartTellsNoChannelOnline()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var channel = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Scenario.Start), null, [channel], ct);

        var resumed = await engine.RecoverAsync(ct);
        await engine.DisposeAsync();

        Assert.False(resumed);
        Assert.Empty(channel.Online);
    }

    /// <summary>
    /// 圏外明けの伝達で1つのチャンネルが例外を投げても、ほかのチャンネルには伝わり、エンジンは止まらない。
    /// </summary>
    [Fact]
    public async Task ThrowingChannelDoesNotStopTheOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var lastApplied = LastApplied(await FirstRunAsync(temp.FilePath, StopAt, ct));

        var recording = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Now), null, [new ThrowingChannel("broken"), recording], ct);
        await engine.RecoverAsync(ct);
        await engine.DisposeAsync();

        AssertOnline(recording, lastApplied, Now);
    }

    /// <summary>圏外明けの通知が1回だけ、指定の区間で届いたことを確かめる。</summary>
    /// <param name="channel">確かめるチャンネル。</param>
    /// <param name="offlineFrom">区間の始まり。</param>
    /// <param name="onlineAt">区間の終わり。</param>
    private static void AssertOnline(RecordingChannel channel, DateTimeOffset offlineFrom, DateTimeOffset onlineAt)
    {
        var online = Assert.Single(channel.Online);
        Assert.Equal(offlineFrom, online.OfflineFrom);
        Assert.Equal(onlineAt, online.OnlineAt);
    }

    /// <summary>
    /// 投稿が出る窓の追いつきでは、LLM を1度も呼ばず、投稿の行動（post_sns）も追いつき中には始まらない。
    /// SNS の行動は候補から外れている（重みが 0）ので、追いつき中の投稿は起きない。
    /// </summary>
    [Fact]
    public async Task CatchUpInThePostingWindowCallsNoLlmAndStartsNoPost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        await FirstRunWithPostsAsync(temp.FilePath, StopAt, ct);

        var llm = new CountingLlm(PostingLlm());
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Now), llm, [], ct);
        await using (engine)
        {
            await engine.RecoverAsync(ct);

            Assert.Equal(0, llm.Calls);
            Assert.DoesNotContain(engine.Log, entry => entry.Event.Origin == Origin.Catchup && entry.Event.EventType == EventTypes.ActionStarted && ((ActionStartedPayload)entry.Event.Payload).ActionId == "post_sns");
        }
    }

    /// <summary>
    /// 止めずに始めて、<paramref name="until"/> まで回す。適用の記録を返す（破棄の前に読む）。世界とペルソナを生成してから回す。
    /// </summary>
    /// <param name="path">保存先。</param>
    /// <param name="until">回す先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    internal static async Task<IReadOnlyList<LoggedEvent>> FirstRunAsync(string path, DateTimeOffset until, CancellationToken ct)
    {
        var engine = await OpenAsync(path, new VirtualClock(Scenario.Start), null, [], ct);
        await using (engine)
        {
            await InjectWorldAndPersonasAsync(engine, ct);
            await engine.RunUntilAsync(until, ct);
            return engine.Log.ToList();
        }
    }

    /// <summary>
    /// <see cref="FirstRunAsync"/> と同じだが、投稿の文面を台本で作り、配信はしない（未送信の操作を残す）。
    /// </summary>
    /// <param name="path">保存先。</param>
    /// <param name="until">回す先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<IReadOnlyList<LoggedEvent>> FirstRunWithPostsAsync(string path, DateTimeOffset until, CancellationToken ct)
    {
        var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        await using var engine = SimEngine.Create(TestWorld.LoadOrThrow(), new SqliteStorage(database), new SplitMixRandomSource(), new VirtualClock(Scenario.Start), null, TestHolidays.Instance, PostingLlm(), [], dispatch: false);
        await InjectWorldAndPersonasAsync(engine, ct);
        await engine.RunUntilAsync(until, ct);
        return engine.Log.ToList();
    }

    /// <summary>
    /// 保存先から起動し直し、<paramref name="until"/> の時計で追いつき、そのまま <paramref name="until"/> まで回す。適用の記録（この起動の分）を返す。
    /// </summary>
    /// <param name="path">保存先。</param>
    /// <param name="until">いまに着く時刻であり、回す先の時刻。</param>
    /// <param name="llm">LLM。null なら使えない実装。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<IReadOnlyList<LoggedEvent>> ResumeAsync(string path, DateTimeOffset until, ILlmClient? llm, CancellationToken ct)
    {
        var engine = await OpenAsync(path, new VirtualClock(until), llm, [], ct);
        await using (engine)
        {
            await engine.RecoverAsync(ct);
            await engine.RunUntilAsync(until, ct);
            return engine.Log.ToList();
        }
    }

    /// <summary>保存先を開いて、エンジンを組む。エンジンを破棄すると、保存先も閉じる。</summary>
    /// <param name="path">保存先。</param>
    /// <param name="clock">時計。</param>
    /// <param name="llm">LLM。null なら使えない実装。</param>
    /// <param name="channels">つなぐチャンネル。</param>
    /// <param name="ct">キャンセル。</param>
    internal static async Task<SimEngine> OpenAsync(string path, IClock clock, ILlmClient? llm, IReadOnlyList<IChannel> channels, CancellationToken ct)
    {
        var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        return SimEngine.Create(TestWorld.LoadOrThrow(), new SqliteStorage(database), new SplitMixRandomSource(), clock, null, TestHolidays.Instance, llm, channels);
    }

    /// <summary>世界と2人のペルソナを、テストと同じ順に投入する。</summary>
    /// <param name="engine">投入先。</param>
    /// <param name="ct">キャンセル。</param>
    internal static async Task InjectWorldAndPersonasAsync(SimEngine engine, CancellationToken ct)
    {
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test, Scenario.Start), ct);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("taro"), new PlaceId("taro_room"), Origin.Test, Scenario.Start), ct);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("hanako"), new PlaceId("home_living"), Origin.Test, Scenario.Start), ct);
    }

    /// <summary>投稿の文面を台本で返す LLM。</summary>
    private static ScriptedLlm PostingLlm() => new ScriptedLlm().Reply("post.compose", "今日の授業眠い");

    /// <summary>適用の記録の最後の適用時刻（状態の最後の適用と同じ）。</summary>
    /// <param name="log">適用の記録。</param>
    private static DateTimeOffset LastApplied(IEnumerable<LoggedEvent> log) =>
        log.Where(entry => entry.Status == EventStatus.Applied).Max(entry => entry.Event.Time);

    /// <summary>
    /// 適用の記録から、発生元を除いた事実の列を作る。連番、時刻、種類、状態、適用番号、参照する連番、段階、中身（JSON）。
    /// 投稿の代わりの出力の理由は、正規化してから比べる（追いつき中は圏外になるので、理由が違う）。
    /// </summary>
    /// <param name="log">適用の記録。</param>
    internal static (long Seq, DateTimeOffset Time, string Type, EventStage Stage, EventStatus Status, long? ApplyNo, long? Cause, string Payload)[] Facts(IEnumerable<LoggedEvent> log) =>
        log.Select(entry => (
            entry.Event.Seq,
            entry.Event.Time,
            entry.Event.EventType,
            entry.Event.Stage,
            entry.Status,
            entry.ApplyNo,
            entry.Event.CauseSeq,
            PayloadJson.Serialize(entry.Event.EventType, Normalize(entry.Event.Payload))))
            .ToArray();

    /// <summary>投稿の代わりの出力の理由を、既定の理由に直す。ほかの中身はそのまま。</summary>
    /// <param name="payload">中身。</param>
    private static EventPayload Normalize(EventPayload payload) =>
        payload is PostSkippedPayload skipped ? skipped with { Reason = PostSkippedPayload.PostSkipReason.GenerationFailed } : payload;

    /// <summary>圏外明けの伝達で例外を投げるチャンネル。</summary>
    private sealed class ThrowingChannel(string kind) : IChannel
    {
        /// <inheritdoc />
        public string Kind { get; } = kind;

        /// <inheritdoc />
        public Task StartAsync(VivaceEngine engine, CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public Task<SendResult> SendAsync(OutboundRequest request, CancellationToken ct) => Task.FromResult<SendResult>(new SendResult.Sent("x"));

        /// <inheritdoc />
        public Task OnlineAsync(DateTimeOffset offlineFrom, DateTimeOffset onlineAt, CancellationToken ct) => throw new InvalidOperationException("壊れている");
    }

    /// <summary>呼ばれた回数を数える LLM の包み。</summary>
    internal sealed class CountingLlm(ILlmClient inner) : ILlmClient
    {
        /// <summary>呼ばれた回数。</summary>
        private int _calls;

        /// <summary>呼ばれた回数。</summary>
        public int Calls => Volatile.Read(ref _calls);

        /// <inheritdoc />
        public Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return inner.GenerateTextAsync(request, ct);
        }
    }
}
