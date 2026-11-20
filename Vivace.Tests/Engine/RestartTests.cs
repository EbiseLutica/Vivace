using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Llm;
using Vivace.Core.Randomness;
using Vivace.Core.Storage;
using Vivace.Core.Time;
using Vivace.Storage.Sqlite;
using Vivace.Testing;
using Vivace.Tests.Storage;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// 止めて SQLite から起動し直しても、事実のイベントの列（適用番号の順）が止めなかったときと一致する。
/// 止める位置は、シードを固定して選ぶ（ハンドラを起こした直後の位置を、半分ほど混ぜる）。
/// </summary>
public class RestartTests
{
    /// <summary>回す日数。3 日あれば、Decider、Planner、効果のハンドラ（投稿）、睡眠の記憶の整理を、どれも通る。</summary>
    private const int Days = 3;

    /// <summary>止める位置を選ぶシード。</summary>
    private const ulong PositionSeed = 20261101;

    /// <summary>それ以外のコミットの後から、固定シードで選ぶ数。</summary>
    private const int ExtraPositions = 3;

    /// <summary>投入の回数（ワールドと2人の生成）。この回までのコミットでは止めない。</summary>
    private const int InjectionCommits = 3;

    /// <summary>
    /// ハンドラを起こすイベントの種類。種類ごとに、止める位置を1つ以上入れる。
    /// plan.due はテストの町では出ないので、ここには入れず、シグナルの対応を単体で確かめる（ProcessingSignalTests）。
    /// </summary>
    private static readonly string[] TriggerKinds =
    [
        EventTypes.PersonaCreated,
        EventTypes.BlockStarted,
        EventTypes.DecisionRequested,
        EventTypes.ActionStarted,
    ];

    /// <summary>投稿の文面の台本（LLM あり）。</summary>
    private static readonly Func<ScriptedLlm> Llm = () => new ScriptedLlm().Reply("post.compose", "今日の授業眠い");

    /// <summary>止めなかった場合の events の全行と比べ、いくつかの位置で止めて起動し直したとき、全行が一致する。</summary>
    [Fact]
    public async Task RestartAtFixedRandomPositionsGivesTheSameFacts()
    {
        var ct = TestContext.Current.CancellationToken;
        using var referenceTemp = new TempDatabase();
        var shapes = await FirstRunAsync(referenceTemp.FilePath, stopAfter: null, ct);
        var expected = await ReadRowsAsync(referenceTemp.FilePath, ct);
        var positions = StopPositions(shapes);

        Assert.NotEmpty(expected);
        Assert.NotEmpty(positions);
        foreach (var position in positions)
        {
            using var temp = new TempDatabase();
            var stopped = await FirstRunAsync(temp.FilePath, position, ct);
            Assert.Equal(position, stopped.Count);
            await RestartAsync(temp.FilePath, ct);

            Assert.Equal(expected, await ReadRowsAsync(temp.FilePath, ct));
        }
    }

    /// <summary>
    /// 起動の直後に一度だけ呼べる。投入や適用のあとに呼ぶと、例外になる。
    /// </summary>
    [Fact]
    public async Task RecoverRefusesToRunAfterInjection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempDatabase();
        var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var engine = SimEngine.Create(TestWorld.LoadOrThrow(), new SqliteStorage(database), new SplitMixRandomSource(), new VirtualClock(Scenario.Start), null, TestHolidays.Instance, Llm(), [new NullChannel(Scenario.ChannelKind)]);
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test, Scenario.Start), ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecoverAsync(ct));
    }

    /// <summary>
    /// 最初の実行をする。stopAfter 回目のコミットのあとで止め、止めなければ最後まで回す。コミットの性質の一覧を返す。
    /// 止めたら、エンジンは破棄して、保存先（データベース）は閉じる。
    /// </summary>
    private static async Task<IReadOnlyList<CommitProbe.Shape>> FirstRunAsync(string path, int? stopAfter, CancellationToken ct)
    {
        var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        var probe = new CommitProbe(new SqliteStorage(database), stopAfter);
        await using var engine = SimEngine.Create(TestWorld.LoadOrThrow(), probe, new SplitMixRandomSource(), new VirtualClock(Scenario.Start), null, TestHolidays.Instance, Llm(), [new NullChannel(Scenario.ChannelKind)]);
        try
        {
            await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test, Scenario.Start), ct);
            await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("taro"), new PlaceId("taro_room"), Origin.Test, Scenario.Start), ct);
            await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("hanako"), new PlaceId("home_living"), Origin.Test, Scenario.Start), ct);
            await engine.RunUntilAsync(Scenario.Start.AddDays(Days), ct);
        }
        catch (CommitStopped)
        {
        }

        return probe.Shapes;
    }

    /// <summary>保存先から起動し直し、最後まで回す。</summary>
    private static async Task RestartAsync(string path, CancellationToken ct)
    {
        var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        await using var engine = SimEngine.Create(TestWorld.LoadOrThrow(), new SqliteStorage(database), new SplitMixRandomSource(), new VirtualClock(Scenario.Start), null, TestHolidays.Instance, Llm(), [new NullChannel(Scenario.ChannelKind)]);
        await engine.RecoverAsync(ct);
        await engine.RunUntilAsync(Scenario.Start.AddDays(Days), ct);
    }

    /// <summary>
    /// events の全行を seq の順に、1行1件の文字列にして読む（行の状態、理由、適用番号、連番の参照、発生元、中身まで）。
    /// 適用の実時刻（applied_utc）は実行のたびに違うので含めない。
    /// </summary>
    private static async Task<string[]> ReadRowsAsync(string path, CancellationToken ct)
    {
        await using var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        await using var command = database.Connection.CreateCommand();
        command.CommandText = """
            SELECT seq, time_utc, event_type, world_id, persona_id, status, status_reason, apply_no,
                   batch_seq, cause_seq, origin, payload, guard, enqueued_utc
            FROM events
            ORDER BY seq
            """;
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var columns = Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "NULL" : reader.GetValue(index).ToString());
            rows.Add(string.Join("|", columns));
        }

        return rows.ToArray();
    }

    /// <summary>
    /// 止める位置を選ぶ。トリガーの種類ごとに1か所ずつと、固定シードでそれ以外から数か所。投入の途中と、未送信の操作が残るコミットは除く。
    /// </summary>
    private static List<int> StopPositions(IReadOnlyList<CommitProbe.Shape> shapes)
    {
        var safe = Candidates(shapes, _ => true);
        var state = PositionSeed;
        var picks = new SortedSet<int>();
        foreach (var kind in TriggerKinds)
        {
            var candidates = Candidates(shapes, shape => shape.Trigger == kind);
            Assert.True(candidates.Count > 0, $"停止位置の候補がない: {kind}");
            picks.Add(candidates[NextIndex(ref state, candidates.Count)]);
        }

        for (var i = 0; i < ExtraPositions; i++)
        {
            picks.Add(safe[NextIndex(ref state, safe.Count)]);
        }

        return picks.ToList();
    }

    /// <summary>条件に合うコミットの位置（1 から数える）。投入の途中と、未送信の操作が残るものは除く。</summary>
    private static List<int> Candidates(IReadOnlyList<CommitProbe.Shape> shapes, Func<CommitProbe.Shape, bool> condition) =>
        Enumerable.Range(1, shapes.Count)
            .Where(index => index > InjectionCommits && !shapes[index - 1].InFlight && condition(shapes[index - 1]))
            .ToList();

    /// <summary>決めたシードから、0 以上 count 未満の次の値を取る（線形合同法。テストの位置を決めるためだけ）。</summary>
    private static int NextIndex(ref ulong state, int count)
    {
        state = unchecked((state * 6364136223846793005UL) + 1442695040888963407UL);
        return (int)((state >> 33) % (ulong)count);
    }
}
