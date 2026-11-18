using System.Globalization;
using Vivace.Core;
using Vivace.Core.Domain;
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

    /// <summary>行動の行の本文。"本を読む＠香織の部屋（予定）" の形。</summary>
    /// <param name="started">行動の開始。</param>
    /// <param name="names">表示名を引く表。</param>
    /// <param name="world">行動のワールド。分からなければ null。</param>
    public static string ActionRow(ActionStartedPayload started, DisplayNames names, WorldId? world) =>
        $"{names.Action(started.ActionId)}＠{names.Place(started.Place, world)}（{ReasonLabel(started.Reason)}）";

    /// <summary>副行動の行の本文。"（スマホ）SNS に投稿する" の形。場所は主の行動の行に書くので、書かない。</summary>
    /// <param name="started">副行動の開始。</param>
    /// <param name="names">表示名を引く表。</param>
    public static string SubActionRow(ActionStartedPayload started, DisplayNames names) =>
        $"（スマホ）{names.Action(started.ActionId)}";

    /// <summary>移動の行の本文。"移動 香織の部屋 → 2年A組の教室" の形。</summary>
    /// <param name="travel">移動の開始。</param>
    /// <param name="names">表示名を引く表。</param>
    /// <param name="world">移動のワールド。分からなければ null。</param>
    public static string TravelRow(TravelStartedPayload travel, DisplayNames names, WorldId? world) =>
        $"移動 {names.Place(travel.From, world)} → {names.Place(travel.To, world)}";

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
