using Vivace.Cli;
using Vivace.Core.Time;
using Vivace.Storage.Sqlite;
using Vivace.Testing;
using Vivace.Tests.Storage;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>
/// vivace run --db。記録から続きを動かし、ワールドとペルソナを生成し直さず、連番を続ける。
/// 記録のシードと違うシードは無視して1行知らせる。倍速の時計は、記録の時刻より前から始めない。
/// </summary>
public class RunCommandDbTests
{
    /// <summary>1回目を1日目の終わりまで回して止め、2回目で続きから2日目の終わりまで回す。</summary>
    [Fact]
    public async Task SecondRunContinuesFromTheRecordWithoutRegenerating()
    {
        var ct = TestContext.Current.CancellationToken;
        using var folder = new DefinitionFolder(TestWorld.Files());
        using var temp = new TempDatabase();

        var first = await RunWithDbAsync(folder.Path, temp.FilePath, new VirtualClock(Scenario.Start), Scenario.WorldSeed, Scenario.Start.AddDays(1), ct);
        var before = await ReadRowsAsync(temp.FilePath, ct);

        var second = await RunWithDbAsync(folder.Path, temp.FilePath, new VirtualClock(Scenario.Start.AddDays(1)), Scenario.WorldSeed, Scenario.Start.AddDays(2), ct);
        var after = await ReadRowsAsync(temp.FilePath, ct);

        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.Single(after, row => row.Type == "world.created");
        Assert.Equal(2, after.Count(row => row.Type == "persona.created"));
        Assert.True(after.Count > before.Count);
        Assert.Equal(Enumerable.Range(1, after.Count).Select(seq => (long)seq), after.Select(row => row.Seq));
        Assert.DoesNotContain("投入が拒否", second.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 記録のシードと違うシードを指定したら、記録のシードで続けることを1行書く。同じなら何も書かない。
    /// </summary>
    [Fact]
    public async Task DifferentSeedIsIgnoredWithOneLine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var folder = new DefinitionFolder(TestWorld.Files());
        using var temp = new TempDatabase();
        await RunWithDbAsync(folder.Path, temp.FilePath, new VirtualClock(Scenario.Start), Scenario.WorldSeed, Scenario.Start.AddHours(1), ct);

        var different = await RunWithDbAsync(folder.Path, temp.FilePath, new VirtualClock(Scenario.Start.AddHours(1)), 42, Scenario.Start.AddHours(2), ct);
        var same = await RunWithDbAsync(folder.Path, temp.FilePath, new VirtualClock(Scenario.Start.AddHours(2)), Scenario.WorldSeed, Scenario.Start.AddHours(3), ct);

        Assert.Contains($"記録のシード {Scenario.WorldSeed} で続ける（--seed 42 は無視した）\n", different.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("で続ける", same.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 倍速の時計は、記録の時刻より前から始めない。記録が先に進んでいるときは、記録の時刻から始める。
    /// 記録が未来でなければ、実時間の時計と倍速の時計は、いまから始める。
    /// </summary>
    [Fact]
    public void ScaledClockNeverStartsBeforeTheRecordedTime()
    {
        var recorded = DateTimeOffset.UtcNow.AddDays(10);

        var scaled = RunCommand.ClockFor(600, recorded);
        Assert.True(scaled.UtcNow >= recorded);
        Assert.IsType<ScaledClock>(RunCommand.ClockFor(1, recorded));

        var past = DateTimeOffset.UtcNow.AddDays(-10);
        Assert.True(RunCommand.ClockFor(600, past).UtcNow > past.AddDays(1));
        Assert.IsType<SystemClock>(RunCommand.ClockFor(1, past));
    }

    /// <summary>
    /// SQLite の記録に対して run を1回回し、終了コードと流れた出力を返す。エンジンが閉じるので、保存先は呼び出しごとに開く。
    /// </summary>
    /// <param name="dir">定義のフォルダ。</param>
    /// <param name="path">記録のファイル。</param>
    /// <param name="clock">時計。</param>
    /// <param name="seed">指定のシード。</param>
    /// <param name="until">回す先の時刻。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<(int Code, string Text)> RunWithDbAsync(string dir, string path, IClock clock, ulong seed, DateTimeOffset until, CancellationToken ct)
    {
        var output = new StringWriter { NewLine = "\n" };
        await using var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        var code = await RunCommand.RunAsync(dir, clock, seed, false, output, ct, until, storage: new SqliteStorage(database));
        return (code, output.ToString());
    }

    /// <summary>記録の全行を、連番の順に読む（未適用の行も含む）。</summary>
    /// <param name="path">記録のファイル。</param>
    /// <param name="ct">キャンセル。</param>
    private static async Task<List<(long Seq, string Type)>> ReadRowsAsync(string path, CancellationToken ct)
    {
        await using var database = await SqliteDatabase.OpenAsync(path, ct: ct);
        await using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT seq, event_type FROM events ORDER BY seq";
        var rows = new List<(long Seq, string Type)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1)));
        }

        return rows;
    }
}
