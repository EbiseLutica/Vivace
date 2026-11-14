using System.Globalization;
using System.Text;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Cli;

/// <summary>
/// 保存先の記録から、日ごと（現地の日付）、人ごと（ID の序数順）の行動表を作る。末尾に、適用、捨てた、却下の件数を付ける。
/// </summary>
public static class DryRunReport
{
    /// <summary>曜日の表記。DayOfWeek の値（日曜が 0）で引く。</summary>
    private static readonly string[] Weekdays = ["日", "月", "火", "水", "木", "金", "土"];

    /// <summary>終わりの記録がない行の、終わりの表記。</summary>
    private const string Unfinished = "未終了";

    /// <summary>記録から行動表と件数を組み立てる。改行は LF。</summary>
    /// <param name="records">保存先の記録（追加された順）。</param>
    /// <param name="utcOffsets">ワールドごとの現地の時差。記録にないワールドは UTC とみなす。</param>
    public static string Render(IEnumerable<LoggedEvent> records, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets)
    {
        var list = records.ToList();
        var rows = BuildRows(list.Where(entry => entry.Status == EventStatus.Applied));
        var dates = rows.Select(row => LocalDate(row, utcOffsets)).Distinct().Order().ToList();
        var personas = rows.Select(row => row.Persona).Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToList();

        var builder = new StringBuilder();
        foreach (var date in dates)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append('（').Append(Weekdays[(int)date.DayOfWeek]).Append("）\n");
            foreach (var persona in personas)
            {
                var personaRows = rows.Where(row => row.Persona == persona && LocalDate(row, utcOffsets) == date).ToList();
                if (personaRows.Count == 0)
                {
                    continue;
                }

                builder.Append(persona.Value).Append('\n');
                foreach (var row in personaRows)
                {
                    builder.Append("  ").Append(FormatRow(row, utcOffsets)).Append('\n');
                }
            }
        }

        if (dates.Count > 0)
        {
            builder.Append('\n');
        }

        var applied = list.Count(entry => entry.Status == EventStatus.Applied);
        var skipped = list.Count(entry => entry.Status == EventStatus.Skipped);
        var rejected = list.Count(entry => entry.Status == EventStatus.Rejected);
        builder.Append("適用 ").Append(Number(applied)).Append(" 件、捨てた ").Append(Number(skipped))
            .Append(" 件、却下 ").Append(Number(rejected)).Append(" 件\n");
        return builder.ToString();
    }

    /// <summary>
    /// 適用されたイベントから、行動と移動の行を作る。同じ人の終わりの記録で閉じる。途中で次の行が始まったら、その開始の時刻で閉じる。
    /// </summary>
    private static List<Row> BuildRows(IEnumerable<LoggedEvent> applied)
    {
        var rows = new List<Row>();
        var open = new Dictionary<PersonaId, Row>();
        foreach (var logged in applied)
        {
            var ev = logged.Event;
            if (ev.PersonaId is not { } persona)
            {
                continue;
            }

            switch (ev.Payload)
            {
                case ActionStartedPayload started:
                    CloseAt(persona, ev.Time, rows, open);
                    open[persona] = new Row(persona, ev.WorldId, ev.Time, null, RowKind.Action, $"[{ReasonLabel(started.Reason)}] {started.ActionId} @{started.Place.Value}");
                    break;
                case ActionEndedPayload when open.TryGetValue(persona, out var action) && action.Kind == RowKind.Action:
                    rows.Add(action with { End = ev.Time });
                    open.Remove(persona);
                    break;
                case TravelStartedPayload travel:
                    CloseAt(persona, ev.Time, rows, open);
                    open[persona] = new Row(persona, ev.WorldId, ev.Time, null, RowKind.Travel, $"移動 {travel.From.Value} → {travel.To.Value}");
                    break;
                case TravelArrivedPayload when open.TryGetValue(persona, out var trip) && trip.Kind == RowKind.Travel:
                    rows.Add(trip with { End = ev.Time });
                    open.Remove(persona);
                    break;
            }
        }

        foreach (var persona in open.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToList())
        {
            rows.Add(open[persona]);
        }

        return rows;
    }

    /// <summary>人の開いている行があれば、時刻 at で閉じて行の列に移す。</summary>
    private static void CloseAt(PersonaId persona, DateTimeOffset at, List<Row> rows, Dictionary<PersonaId, Row> open)
    {
        if (open.Remove(persona, out var pending))
        {
            rows.Add(pending with { End = at });
        }
    }

    /// <summary>行を "開始-終わり 本文" の形にする。終わりの記録がなければ「未終了」。</summary>
    private static string FormatRow(Row row, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets)
    {
        var offset = OffsetOf(row, utcOffsets);
        var start = TimeOf(row.Start, offset);
        var end = row.End is { } finished ? TimeOf(finished, offset) : Unfinished;
        return $"{start}-{end} {row.Body}";
    }

    /// <summary>行の開始の現地の日付。</summary>
    private static DateOnly LocalDate(Row row, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets) =>
        LocalTimeHelper.ToLocal(row.Start, OffsetOf(row, utcOffsets)).Date;

    /// <summary>行のワールドの現地の時差。ワールドがなければ UTC。</summary>
    private static TimeSpan OffsetOf(Row row, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets) =>
        row.World is { } world && utcOffsets.TryGetValue(world, out var offset) ? offset : TimeSpan.Zero;

    /// <summary>UTC の時刻を、現地の "HH:mm" にする。</summary>
    private static string TimeOf(DateTimeOffset utc, TimeSpan offset) =>
        LocalTimeHelper.ToLocal(utc, offset).Time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>行動の理由の表記。</summary>
    private static string ReasonLabel(ActivityReason reason) => reason switch
    {
        ActivityReason.Block => "予定",
        ActivityReason.Chosen => "自由",
        ActivityReason.Reflex => "反射",
        ActivityReason.Late => "遅刻",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    /// <summary>整数の表記（カルチャに依らない）。</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>行の種類。行動と移動は、それぞれの終わりの記録で閉じる。</summary>
    private enum RowKind
    {
        /// <summary>行動。action.ended で閉じる。</summary>
        Action,

        /// <summary>移動。travel.arrived で閉じる。</summary>
        Travel,
    }

    /// <summary>表の1行。End が null なら、記録の終わりまで続いている（未終了）。</summary>
    /// <param name="Persona">人。</param>
    /// <param name="World">人のワールド。記録になければ null。</param>
    /// <param name="Start">開始の時刻（UTC）。</param>
    /// <param name="End">終わりの時刻（UTC）。終わりの記録があればその時刻、途中で次の行が始まったらその開始の時刻、記録の終わりまで続いていれば null。</param>
    /// <param name="Kind">行の種類。</param>
    /// <param name="Body">本文（行動の表記、または移動の表記）。</param>
    private sealed record Row(PersonaId Persona, WorldId? World, DateTimeOffset Start, DateTimeOffset? End, RowKind Kind, string Body);
}
