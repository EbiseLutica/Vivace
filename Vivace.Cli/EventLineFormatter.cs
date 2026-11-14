using System.Globalization;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Cli;

/// <summary>流れる出力の1行を作る。形は "MM-dd HH:mm 人 内容"。時刻はワールドの現地の時刻にする。</summary>
public static class EventLineFormatter
{
    /// <summary>
    /// 適用されたイベントを1行にする。既定では行動と移動の開始と終わりだけを出し、それ以外は null を返す。
    /// all のときは、すべてのイベントを出し、行動と移動以外はイベントタイプの名前だけにする。
    /// </summary>
    /// <param name="logged">適用の記録。</param>
    /// <param name="utcOffsets">ワールド ID ごとの現地の時差。記録にないワールドは UTC とみなす。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    public static string? Format(LoggedEvent logged, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets, bool all)
    {
        var ev = logged.Event;
        var body = Body(ev, all);
        if (body is null)
        {
            return null;
        }

        var offset = ev.WorldId is { } world && utcOffsets.TryGetValue(world, out var found) ? found : TimeSpan.Zero;
        var (date, time) = LocalTimeHelper.ToLocal(ev.Time, offset);
        var who = ev.PersonaId?.Value ?? ev.WorldId?.Value ?? "-";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{date:MM-dd} {time:HH:mm} {who} {body}");
    }

    /// <summary>イベントの内容の表記。出さないイベントは null。</summary>
    /// <param name="ev">イベント。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    private static string? Body(SimEvent ev, bool all)
    {
        switch (ev.Payload)
        {
            case ActionStartedPayload started:
                return ReportText.ActionBody(started);
            case ActionEndedPayload ended:
                return string.IsNullOrEmpty(ended.ActionId) ? "行動の終わり" : $"終わり {ended.ActionId}";
            case TravelStartedPayload travel:
                return ReportText.TravelBody(travel);
            case TravelArrivedPayload arrived:
                return $"到着 {arrived.Place.Value}";
            default:
                return all ? ev.EventType : null;
        }
    }
}
