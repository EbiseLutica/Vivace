using System.Globalization;
using Vivace.Core;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;

namespace Vivace.Cli;

/// <summary>行動表と流れる出力で共通の表記。理由、行動と移動の本文、適用、捨てた、却下の件数。</summary>
public static class ReportText
{
    /// <summary>曜日の表記。DayOfWeek の値（日曜が 0）で引く。</summary>
    private static readonly string[] Weekdays = ["日", "月", "火", "水", "木", "金", "土"];

    /// <summary>行動の理由の表記。</summary>
    /// <param name="reason">理由。</param>
    public static string ReasonLabel(ActivityReason reason) => reason switch
    {
        ActivityReason.Block => "予定",
        ActivityReason.Chosen => "自由",
        ActivityReason.Reflex => "反射",
        ActivityReason.Late => "遅刻",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    /// <summary>
    /// 曜日の表記（一字）。DayOfWeek の値（日曜が 0）で引く。
    /// </summary>
    /// <param name="date">日付。</param>
    public static string Weekday(DateOnly date) => Weekdays[(int)date.DayOfWeek];

    /// <summary>行動の開始の本文。"[予定] attend_class @school_2a" の形。</summary>
    /// <param name="started">行動の開始。</param>
    public static string ActionBody(ActionStartedPayload started) =>
        $"[{ReasonLabel(started.Reason)}] {started.ActionId} @{started.Place.Value}";

    /// <summary>移動の本文。"移動 kaori_room → school_2a" の形。</summary>
    /// <param name="travel">移動の開始。</param>
    public static string TravelBody(TravelStartedPayload travel) =>
        $"移動 {travel.From.Value} → {travel.To.Value}";

    /// <summary>記録の件数の行。"適用 N 件、捨てた N 件、却下 N 件" に改行を付けて返す。</summary>
    /// <param name="records">保存先の記録。</param>
    public static string CountLine(IEnumerable<LoggedEvent> records)
    {
        var list = records.ToList();
        var applied = list.Count(entry => entry.Status == EventStatus.Applied);
        var skipped = list.Count(entry => entry.Status == EventStatus.Skipped);
        var rejected = list.Count(entry => entry.Status == EventStatus.Rejected);
        return string.Create(CultureInfo.InvariantCulture, $"適用 {applied} 件、捨てた {skipped} 件、却下 {rejected} 件\n");
    }
}
