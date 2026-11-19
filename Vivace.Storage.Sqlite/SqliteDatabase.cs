using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// SQLite のファイルを開き、接続の設定（WAL、同期、待ち時間、外部キー）とマイグレーションを済ませた接続を持つ。
/// </summary>
public sealed class SqliteDatabase : IAsyncDisposable
{
    /// <summary>待ち時間（ミリ秒）。ロックの競合の待ち。</summary>
    private const int BusyTimeoutMs = 5000;

    private SqliteDatabase(SqliteConnection connection)
    {
        Connection = connection;
    }

    /// <summary>開いている接続。</summary>
    internal SqliteConnection Connection { get; }

    /// <summary>
    /// ファイルを開いて接続を設定し、足りない版を当てる。ファイルがなければ作る。
    /// </summary>
    /// <param name="path">データベースのファイルのパス。</param>
    /// <param name="migrations">版の順の SQL。省略するとアセンブリに埋め込んだもの。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<SqliteDatabase> OpenAsync(string path, IReadOnlyList<SqliteMigration>? migrations = null, CancellationToken ct = default)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            await connection.OpenAsync(ct);
            await ConfigureAsync(connection, ct);
            await SqliteMigrator.MigrateAsync(connection, migrations ?? SqliteMigrator.Embedded(), ct);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return new SqliteDatabase(connection);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();

    /// <summary>接続の設定。WAL、同期は NORMAL、待ち時間 5 秒、外部キーを有効にする。</summary>
    private static async Task ConfigureAsync(SqliteConnection connection, CancellationToken ct)
    {
        var pragmas = new[]
        {
            $"PRAGMA busy_timeout = {BusyTimeoutMs.ToString(CultureInfo.InvariantCulture)}",
            "PRAGMA journal_mode = WAL",
            "PRAGMA synchronous = NORMAL",
            "PRAGMA foreign_keys = ON",
        };
        foreach (var pragma in pragmas)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = pragma;
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
