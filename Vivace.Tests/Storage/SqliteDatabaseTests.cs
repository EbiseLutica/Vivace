using Microsoft.Data.Sqlite;
using Vivace.Storage.Sqlite;

namespace Vivace.Tests.Storage;

/// <summary>SQLite の接続の設定と、マイグレーション（版の上げ方、再適用、失敗時の巻き戻し）。</summary>
public class SqliteDatabaseTests
{
    /// <summary>新しいファイルを開くと、版は 1 になり、状態のテーブルがそろう。</summary>
    [Fact]
    public async Task NewDatabaseIsMigratedToVersionOne()
    {
        using var temp = new TempDatabase();
        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, await SqliteMigrator.ReadVersionAsync(database.Connection, TestContext.Current.CancellationToken));
        Assert.Equal(
            new[] { "events", "inbox", "incidents", "outbound", "personas", "world_places", "world_terms", "worlds" },
            await TableNamesAsync(database.Connection));
    }

    /// <summary>接続の設定は、WAL、同期 NORMAL（1）、外部キー有効（1）、待ち時間 5000 ミリ秒。</summary>
    [Fact]
    public async Task ConnectionSettingsAreApplied()
    {
        using var temp = new TempDatabase();
        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: TestContext.Current.CancellationToken);

        Assert.Equal("wal", await ScalarAsync(database.Connection, "PRAGMA journal_mode"));
        Assert.Equal(1L, await ScalarAsync(database.Connection, "PRAGMA synchronous"));
        Assert.Equal(1L, await ScalarAsync(database.Connection, "PRAGMA foreign_keys"));
        Assert.Equal(5000L, await ScalarAsync(database.Connection, "PRAGMA busy_timeout"));
    }

    /// <summary>開き直しても版は上がらず、中のデータは残る（同じ版を2回当てない）。</summary>
    [Fact]
    public async Task ReopeningDoesNotApplyMigrationsAgain()
    {
        using var temp = new TempDatabase();
        await using (var first = await SqliteDatabase.OpenAsync(temp.FilePath, ct: TestContext.Current.CancellationToken))
        {
            await ExecuteAsync(first.Connection, "INSERT INTO worlds (id, def_id, seed, status, graph_version, created_seq) VALUES ('w1', 'testville', 1, 'alive', 0, 1)");
        }

        await using var second = await SqliteDatabase.OpenAsync(temp.FilePath, ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, await SqliteMigrator.ReadVersionAsync(second.Connection, TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ScalarAsync(second.Connection, "SELECT COUNT(*) FROM worlds"));
    }

    /// <summary>版が上がったとき、足りない版だけを当て、既存の表とデータは触らない。</summary>
    [Fact]
    public async Task UpgradeAppliesOnlyMissingVersions()
    {
        using var temp = new TempDatabase();
        var first = new[] { new SqliteMigration(1, "one", "CREATE TABLE t1 (x INTEGER);") };
        await using (var before = await SqliteDatabase.OpenAsync(temp.FilePath, first, TestContext.Current.CancellationToken))
        {
            await ExecuteAsync(before.Connection, "INSERT INTO t1 (x) VALUES (5)");
        }

        var second = new[] { first[0], new SqliteMigration(2, "two", "CREATE TABLE t2 (y INTEGER);") };
        await using var after = await SqliteDatabase.OpenAsync(temp.FilePath, second, TestContext.Current.CancellationToken);

        Assert.Equal(2, await SqliteMigrator.ReadVersionAsync(after.Connection, TestContext.Current.CancellationToken));
        Assert.Equal(1L, await ScalarAsync(after.Connection, "SELECT COUNT(*) FROM t1"));
        Assert.Equal(0L, await ScalarAsync(after.Connection, "SELECT COUNT(*) FROM t2"));
    }

    /// <summary>途中の版が失敗したら、同じトランザクションの前の版も残さない（版は上がらない）。</summary>
    [Fact]
    public async Task FailedUpgradeRollsBackTheWholeBatch()
    {
        using var temp = new TempDatabase();
        var first = new[] { new SqliteMigration(1, "one", "CREATE TABLE t1 (x INTEGER);") };
        await using (var before = await SqliteDatabase.OpenAsync(temp.FilePath, first, TestContext.Current.CancellationToken))
        {
        }

        var broken = new[]
        {
            first[0],
            new SqliteMigration(2, "two", "CREATE TABLE t2 (y INTEGER);"),
            new SqliteMigration(3, "three", "CREATE TABLE t2 (z INTEGER);"),
        };
        await Assert.ThrowsAsync<SqliteException>(() => SqliteDatabase.OpenAsync(temp.FilePath, broken, TestContext.Current.CancellationToken));

        await using var after = await SqliteDatabase.OpenAsync(temp.FilePath, first, TestContext.Current.CancellationToken);
        Assert.Equal(1, await SqliteMigrator.ReadVersionAsync(after.Connection, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "t1" }, await TableNamesAsync(after.Connection));
    }

    /// <summary>埋め込みの SQL は、番号 1 の init の1件で、events などの表を作る。</summary>
    [Fact]
    public void EmbeddedMigrationIsVersionOneInit()
    {
        var migrations = SqliteMigrator.Embedded();

        var only = Assert.Single(migrations);
        Assert.Equal(1, only.Version);
        Assert.Equal("init", only.Name);
        Assert.Contains("CREATE TABLE events", only.Sql, StringComparison.Ordinal);
    }

    /// <summary>テーブルの名前の一覧（序数の昇順）。</summary>
    private static async Task<string[]> TableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    /// <summary>1行1列の値を読む。</summary>
    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    /// <summary>SQL を1回実行する。</summary>
    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
