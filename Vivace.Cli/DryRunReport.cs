using System.Globalization;
using System.Text;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Core.Time;

namespace Vivace.Cli;

/// <summary>
/// 保存先の記録から、日ごと（現地の日付）、人ごと（ID の序数順）の行動表を作る。末尾に、適用、捨てた、却下の件数を付ける。
/// </summary>
public static class DryRunReport
{
    /// <summary>終わりの記録がない行の、終わりの表記。</summary>
    private const string Unfinished = "未終了";

    /// <summary>記録から行動表と件数を組み立てる。改行は LF。</summary>
    /// <param name="records">保存先の記録（追加された順）。</param>
    /// <param name="names">表示名を引く表。人、場所、行動は名前で書く。</param>
    /// <param name="utcOffsets">ワールドごとの現地の時差。記録にないワールドは UTC とみなす。</param>
    public static string Render(IEnumerable<LoggedEvent> records, DisplayNames names, IReadOnlyDictionary<WorldId, TimeSpan> utcOffsets)
    {
        var list = records.ToList();
        var rows = BuildRows(list.Where(entry => entry.Status == EventStatus.Applied), names);
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
                .Append('（').Append(ReportText.Weekday(date)).Append("）\n");
            foreach (var persona in personas)
            {
                var personaRows = rows.Where(row => row.Persona == persona && LocalDate(row, utcOffsets) == date).ToList();
                if (personaRows.Count == 0)
                {
                    continue;
                }

                builder.Append(names.Persona(persona)).Append('\n');
                foreach (var row in personaRows)
                {
                    var indent = row.Kind == RowKind.SubAction ? "    " : "  ";
                    builder.Append(indent).Append(FormatRow(row, utcOffsets)).Append('\n');
                }
            }
        }

        if (dates.Count > 0)
        {
            builder.Append('\n');
        }

        builder.Append(ReportText.CountLine(list));
        return builder.ToString();
    }

    /// <summary>
    /// 適用されたイベントから、行動と移動の行を作る。同じ人の終わりの記録で閉じる。途中で次の行が始まったら、その開始の時刻で閉じる。
    /// 副行動の行は、主の行動の下に1段深く並べる（開始の時刻が同じなら、主の行動が先）。
    /// </summary>
    private static List<Row> BuildRows(IEnumerable<LoggedEvent> applied, DisplayNames names)
    {
        var rows = new List<Row>();
        var open = new Dictionary<PersonaId, Row>();
        var openSub = new Dictionary<PersonaId, Row>();
        foreach (var logged in applied)
        {
            var ev = logged.Event;
            if (ev.PersonaId is not { } persona)
            {
                continue;
            }

            switch (ev.Payload)
            {
                case ActionStartedPayload { IsSubAction: true } subStarted:
                    openSub[persona] = new Row(persona, ev.WorldId, ev.Time, null, RowKind.SubAction, ReportText.SubActionRow(subStarted, names));
                    break;
                case ActionStartedPayload started:
                    CloseAt(persona, ev.Time, rows, open, openSub);
                    open[persona] = new Row(persona, ev.WorldId, ev.Time, null, RowKind.Action, ReportText.ActionRow(started, names, ev.WorldId));
                    break;
                case ActionEndedPayload { IsSubAction: true } when openSub.Remove(persona, out var sub):
                    rows.Add(sub with { End = ev.Time });
                    break;
                case ActionEndedPayload { IsSubAction: false } when open.TryGetValue(persona, out var action) && action.Kind == RowKind.Action:
                    CloseSub(persona, ev.Time, rows, openSub);
                    rows.Add(action with { End = ev.Time });
                    open.Remove(persona);
                    break;
                case TravelStartedPayload travel:
                    CloseAt(persona, ev.Time, rows, open, openSub);
                    open[persona] = new Row(persona, ev.WorldId, ev.Time, null, RowKind.Travel, ReportText.TravelRow(travel, names, ev.WorldId));
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

        foreach (var persona in openSub.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToList())
        {
            rows.Add(openSub[persona]);
        }

        return rows.OrderBy(row => row.Start).ThenBy(row => row.Kind == RowKind.SubAction).ToList();
    }

    /// <summary>人の開いている行（主と副行動）があれば、時刻 at で閉じて行の列に移す。</summary>
    private static void CloseAt(PersonaId persona, DateTimeOffset at, List<Row> rows, Dictionary<PersonaId, Row> open, Dictionary<PersonaId, Row> openSub)
    {
        CloseSub(persona, at, rows, openSub);
        if (open.Remove(persona, out var pending))
        {
            rows.Add(pending with { End = at });
        }
    }

    /// <summary>人の開いている副行動があれば、時刻 at で閉じて行の列に移す。</summary>
    private static void CloseSub(PersonaId persona, DateTimeOffset at, List<Row> rows, Dictionary<PersonaId, Row> openSub)
    {
        if (openSub.Remove(persona, out var pending))
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

    /// <summary>行の種類。行動と移動は、それぞれの終わりの記録で閉じる。</summary>
    private enum RowKind
    {
        /// <summary>行動。action.ended で閉じる。</summary>
        Action,

        /// <summary>移動。travel.arrived で閉じる。</summary>
        Travel,

        /// <summary>副行動。その action.ended で閉じる。</summary>
        SubAction,
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
