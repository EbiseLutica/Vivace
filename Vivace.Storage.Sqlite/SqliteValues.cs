using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Data.Sqlite;

namespace Vivace.Storage.Sqlite;

/// <summary>SQLite の値と、C# の値の相互変換（時刻は UTC の Unix ミリ秒、状態の列は JSON）。</summary>
internal static class SqliteValues
{
    /// <summary>時刻を Unix ミリ秒にする。</summary>
    /// <param name="value">時刻。</param>
    public static long ToMs(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    /// <summary>時刻を Unix ミリ秒にする。なければ NULL。</summary>
    /// <param name="value">時刻。</param>
    public static object ToMsOrNull(DateTimeOffset? value) => value is { } time ? ToMs(time) : DBNull.Value;

    /// <summary>Unix ミリ秒を、オフセット 0 の時刻にする。</summary>
    /// <param name="ms">Unix ミリ秒。</param>
    public static DateTimeOffset FromMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms);

    /// <summary>Unix ミリ秒を時刻にする。NULL なら null。</summary>
    /// <param name="ms">Unix ミリ秒。</param>
    public static DateTimeOffset? FromMsOrNull(long? ms) => ms is { } value ? FromMs(value) : null;

    /// <summary>値を JSON の文字列にする。</summary>
    /// <param name="value">値。</param>
    /// <param name="info">JSON の型情報。</param>
    public static string Json<T>(T value, JsonTypeInfo<T> info) => JsonSerializer.Serialize(value, info);

    /// <summary>値を JSON の文字列にする。null なら NULL。</summary>
    /// <param name="value">値。</param>
    /// <param name="info">JSON の型情報。</param>
    public static object JsonOrNull<T>(T? value, JsonTypeInfo<T> info)
        where T : class =>
        value is null ? DBNull.Value : JsonSerializer.Serialize(value, info);

    /// <summary>JSON の文字列から値を作る。</summary>
    /// <param name="text">JSON の文字列。</param>
    /// <param name="info">JSON の型情報。</param>
    public static T ReadJson<T>(string text, JsonTypeInfo<T> info) =>
        JsonSerializer.Deserialize(text, info)!;

    /// <summary>JSON の文字列を、要素として読む（読み終えた後も使えるように複製する）。</summary>
    /// <param name="text">JSON の文字列。</param>
    public static JsonElement ReadElement(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>コマンドに名前つきの値を足す。null は NULL にする。</summary>
    /// <param name="command">コマンド。</param>
    /// <param name="name">パラメータの名前（"$seq" など）。</param>
    /// <param name="value">値。</param>
    public static void Bind(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    /// <summary>SQL を実行し、行を読んで一覧にする。読み終えてから返す（同じ接続で次の SQL を実行できる）。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="sql">SQL。</param>
    /// <param name="bind">パラメータを足す処理。</param>
    /// <param name="map">1行を値にする処理。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<List<T>> QueryAsync<T>(SqliteConnection connection, string sql, Action<SqliteCommand> bind, Func<SqliteDataReader, T> map, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    /// <summary>SQL を、トランザクションの中で実行する。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="sql">SQL。</param>
    /// <param name="bind">パラメータを足す処理。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, Action<SqliteCommand> bind, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        bind(command);
        await command.ExecuteNonQueryAsync(ct);
    }
}
