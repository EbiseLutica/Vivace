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
/// 止めていた間を、仮想の時計で早送りして追いつく。追いつきの間は配信せず、積むイベントの発生元は catchup で、
/// 追いついたあとに圏外明けを伝える。
/// </summary>
public class CatchUpTests
{
    /// <summary>止めるまでの時刻。</summary>
    private static readonly DateTimeOffset StopAt = Scenario.Start.AddDays(1);

    /// <summary>止めていた間を経て、いまに着く時刻。</summary>
    private static readonly DateTimeOffset Now = Scenario.Start.AddDays(2);

    /// <summary>
    /// 止めて時刻を進めてから起動し直すと、止めなかったときと事実の列が一致する。発生元は catchup に変わるので比べない。
    /// 投稿の代わりの出力（post.skipped）の理由は、追いつき中は圏外になるので比べない（LLM を使わないので、どちらも文面は作られない）。
    /// </summary>
    [Fact]
    public async Task CatchUpGivesTheSameFactsAsAnUninterruptedRun()
    {
        var ct = TestContext.Current.CancellationToken;
        using var referenceTemp = new TempDatabase();
        var reference = await FirstRunAsync(referenceTemp.FilePath, Now, ct);

        using var temp = new TempDatabase();
        var first = await FirstRunAsync(temp.FilePath, StopAt, ct);
        var resumed = await ResumeAsync(temp.FilePath, ct);

        Assert.Equal(Facts(reference), Facts([.. first, .. resumed]));
        Assert.Contains(resumed, entry => entry.Event.Origin == Origin.Catchup);
        Assert.DoesNotContain(first, entry => entry.Event.Origin == Origin.Catchup);
    }

    /// <summary>
    /// 追いつきの間は、配信しない。追いついたあとに、チャンネルへ圏外明けを1回伝える。
    /// </summary>
    [Fact]
    public async Task CatchUpSendsNothingAndTellsChannelsOnline()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var first = await FirstRunWithPostsAsync(temp.FilePath, StopAt, ct);
        Assert.Contains(first, entry => entry.Event.EventType == EventTypes.OutboundSpeech);

        var channel = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(Now), PostingLlm(), [channel], ct);
        await engine.RecoverAsync(ct);
        Assert.Empty(channel.Sent);

        await engine.DisposeAsync();
        Assert.Equal(1, channel.OnlineCount);
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
        var lastApplied = first.Max(entry => entry.Event.Time);

        var engine = await OpenAsync(temp.FilePath, new VirtualClock(lastApplied.AddDays(14).AddSeconds(1)), null, [], ct);
        await using (engine)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecoverAsync(ct));

            Assert.Contains("persona.gap", error.Message);
            Assert.Empty(engine.Log);
        }
    }

    /// <summary>
    /// 止めていた間が 1 分以内なら、追いつきに入らず、圏外明けも伝えない。
    /// </summary>
    [Fact]
    public async Task NoCatchUpWithinOneMinuteAndNoOnlineNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var first = await FirstRunAsync(temp.FilePath, StopAt, ct);
        var lastApplied = first.Max(entry => entry.Event.Time);

        var channel = new RecordingChannel(Scenario.ChannelKind);
        var engine = await OpenAsync(temp.FilePath, new VirtualClock(lastApplied.AddSeconds(59)), null, [channel], ct);
        await engine.RecoverAsync(ct);
        Assert.Empty(engine.Log);
        await engine.DisposeAsync();

        Assert.Equal(0, channel.OnlineCount);
    }

    /// <summary>
    /// 止めずに始めて、<paramref name="until"/> まで回す。適用の記録を返す（破棄の前に読む）。世界とペルソナを生成してから回す。
    /// </summary>
    /// <param name="path">保存先。</param>
    /// <param name="until">回す先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<IReadOnlyList<LoggedEvent>> FirstRunAsync(string path, DateTimeOffset until, CancellationToken ct)
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
    /// 保存先から起動し直し、<see cref="Now"/> の時計で追いつき、そのまま <see cref="Now"/> まで回す。適用の記録（この起動の分）を返す。
    /// </summary>
    /// <param name="path">保存先。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<IReadOnlyList<LoggedEvent>> ResumeAsync(string path, CancellationToken ct)
    {
        var engine = await OpenAsync(path, new VirtualClock(Now), null, [], ct);
        await using (engine)
        {
            await engine.RecoverAsync(ct);
            await engine.RunUntilAsync(Now, ct);
            return engine.Log.ToList();
        }
    }

    /// <summary>保存先を開いて、エンジンを組む。エンジンを破棄すると、保存先も閉じる。</summary>
    /// <param name="path">保存先。</param>
    /// <param name="clock">時計。</param>
    /// <param name="llm">LLM。null なら使えない実装。</param>
    /// <param name="channels">つなぐチャンネル。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<SimEngine> OpenAsync(string path, IClock clock, ILlmClient? llm, IReadOnlyList<IChannel> channels, CancellationToken ct)
    {
        var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        return SimEngine.Create(TestWorld.LoadOrThrow(), new SqliteStorage(database), new SplitMixRandomSource(), clock, null, TestHolidays.Instance, llm, channels);
    }

    /// <summary>世界と2人のペルソナを、テストと同じ順に投入する。</summary>
    /// <param name="engine">投入先。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task InjectWorldAndPersonasAsync(SimEngine engine, CancellationToken ct)
    {
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test, Scenario.Start), ct);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("taro"), new PlaceId("taro_room"), Origin.Test, Scenario.Start), ct);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("hanako"), new PlaceId("home_living"), Origin.Test, Scenario.Start), ct);
    }

    /// <summary>投稿の文面を台本で返す LLM。</summary>
    private static ScriptedLlm PostingLlm() => new ScriptedLlm().Reply("post.compose", "今日の授業眠い");

    /// <summary>
    /// 適用の記録から、発生元を除いた事実（連番、時刻、種類、状態、適用番号、中身）の列を作る。投稿の代わりの出力の中身は、種類だけにする。
    /// </summary>
    /// <param name="log">適用の記録。</param>
    private static (long Seq, DateTimeOffset Time, string Type, EventStatus Status, long? ApplyNo, string? Payload)[] Facts(IEnumerable<LoggedEvent> log) =>
        log.Select(entry => (
            entry.Event.Seq,
            entry.Event.Time,
            entry.Event.EventType,
            entry.Status,
            entry.ApplyNo,
            entry.Event.Payload is PostSkippedPayload ? null : PayloadJson.Serialize(entry.Event.EventType, entry.Event.Payload)))
            .ToArray();
}
