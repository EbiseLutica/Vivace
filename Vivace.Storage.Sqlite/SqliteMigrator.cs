using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// スキーマのマイグレーション。版は PRAGMA user_version で持つ。足りない版を、1つのトランザクションで順に当てる。
/// </summary>
public static class SqliteMigrator
{
    /// <summary>アセンブリに埋め込んだ SQL の名前の接頭辞（LogicalName の "migrations/"）。</summary>
    private const string ResourcePrefix = "migrations/";

    /// <summary>アセンブリに埋め込んだ、番号つきの SQL を版の順に返す。</summary>
    public static IReadOnlyList<SqliteMigration> Embedded()
    {
        var assembly = typeof(SqliteMigrator).Assembly;
        var migrations = new List<SqliteMigration>();
        foreach (var resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resource[ResourcePrefix.Length..^".sql".Length];
            var separator = fileName.IndexOf('_', StringComparison.Ordinal);
            if (separator <= 0 || !int.TryParse(fileName[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                throw new InvalidDataException($"マイグレーションの名前が番号で始まっていません: {resource}");
            }

            migrations.Add(new SqliteMigration(version, fileName[(separator + 1)..], ReadText(assembly, resource)));
        }

        return Ordered(migrations);
    }

    /// <summary>接続の版を、足りない分の SQL で上げる。すべて1つのトランザクションで当て、失敗したら何も残さない。</summary>
    /// <param name="connection">開いている接続。</param>
    /// <param name="migrations">版の順の SQL の列。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task MigrateAsync(SqliteConnection connection, IReadOnlyList<SqliteMigration> migrations, CancellationToken ct)
    {
        var ordered = Ordered(migrations.ToList());
        var current = await ReadVersionAsync(connection, ct);
        var pending = ordered.Where(migration => migration.Version > current).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        await using var transaction = connection.BeginTransaction();
        foreach (var migration in pending)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = migration.Sql;
            await command.ExecuteNonQueryAsync(ct);
        }

        await using (var version = connection.CreateCommand())
        {
            version.Transaction = transaction;
            version.CommandText = $"PRAGMA user_version = {pending[^1].Version.ToString(CultureInfo.InvariantCulture)}";
            await version.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <summary>接続の現在の版（PRAGMA user_version）を読む。</summary>
    /// <param name="connection">開いている接続。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        var value = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    /// <summary>版の順に並べ、番号の重複を例外にする。</summary>
    private static IReadOnlyList<SqliteMigration> Ordered(List<SqliteMigration> migrations)
    {
        var ordered = migrations.OrderBy(migration => migration.Version).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].Version == ordered[index - 1].Version)
            {
                throw new InvalidDataException($"マイグレーションの番号が重複しています: {ordered[index].Version}");
            }
        }

        return ordered;
    }

    /// <summary>埋め込みの SQL の文字列を読む。</summary>
    private static string ReadText(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"埋め込みの SQL が見つかりません: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
