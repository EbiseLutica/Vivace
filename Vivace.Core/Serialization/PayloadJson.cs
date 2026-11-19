using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.Serialization;

/// <summary>
/// イベントのペイロードの JSON。型はイベントタイプから引き、JSON の中には型の印を入れない。
/// 知らないイベントタイプと、知らないバージョンは例外にする（握りつぶさない）。
/// </summary>
public static class PayloadJson
{
    /// <summary>ペイロードを JSON の文字列にする。</summary>
    /// <param name="eventType">イベントタイプの名前。</param>
    /// <param name="payload">ペイロード。イベントタイプの型と一致していること。</param>
    public static string Serialize(string eventType, EventPayload payload)
    {
        var info = InfoOf(eventType);
        if (payload.GetType() != info.PayloadType)
        {
            throw new ArgumentException($"ペイロードの型がイベントタイプと一致しません: {eventType} と {payload.GetType().Name}", nameof(payload));
        }

        return JsonSerializer.Serialize(payload, TypeInfoOf(info));
    }

    /// <summary>JSON の文字列から、イベントタイプに対応する型のペイロードを作る。</summary>
    /// <param name="eventType">イベントタイプの名前。</param>
    /// <param name="json">JSON の文字列。</param>
    public static EventPayload Deserialize(string eventType, string json)
    {
        var info = InfoOf(eventType);
        var payload = JsonSerializer.Deserialize(json, TypeInfoOf(info)) as EventPayload
            ?? throw new InvalidDataException($"ペイロードが読めません: {eventType}");
        if (payload.V != SupportedVersion)
        {
            throw new InvalidDataException($"対応していないペイロードのバージョンです: {eventType} v{payload.V}");
        }

        return payload;
    }

    /// <summary>このコードが読み書きするペイロードのバージョン。</summary>
    private const int SupportedVersion = 1;

    /// <summary>イベントタイプの名前から、テーブルの1件を引く。知らなければ例外。</summary>
    private static EventTypeInfo InfoOf(string eventType) =>
        EventTypes.TryGet(eventType, out var info) ? info : throw new UnknownEventTypeException(eventType);

    /// <summary>ペイロードの型の JSON の型情報を引く。</summary>
    private static JsonTypeInfo TypeInfoOf(EventTypeInfo info) =>
        VivaceJsonContext.Default.GetTypeInfo(info.PayloadType)
        ?? throw new InvalidOperationException($"ペイロードの型の JSON の情報がありません: {info.PayloadType.Name}");
}
