using System.Collections.Immutable;
using System.Globalization;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Time;

namespace Vivace.Core.Schedule;

/// <summary>
/// 日付から、その日の固定予定のブロックを出す（D3 3.5）。
/// 期間（最も短いものを採る）、例外日、祝日、入れ子（within）、時間帯の並べ書き（times）を踏まえる。
/// </summary>
public static class FixedScheduleResolver
{
    private const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// ペルソナの1日の固定予定を解決する。
    /// </summary>
    /// <param name="definitions">行動の定義（スマホの既定値を引くため）。</param>
    /// <param name="persona">ペルソナの定義。</param>
    /// <param name="date">現地の日付。</param>
    /// <param name="holidays">ワールドの祝日の暦。</param>
    public static ResolvedDay Resolve(Definitions.Definitions definitions, PersonaDef persona, DateOnly date, IHolidayCalendar holidays)
    {
        var schedule = persona.Schedule;
        var period = PeriodOf(schedule.Periods, date);
        var exception = schedule.Exceptions.FirstOrDefault(item => item.Date == date);
        var context = new DayContext(
            definitions,
            date,
            period,
            holidays.IsHoliday(date),
            exception?.Skip.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
            schedule.Blocks,
            new List<ScheduleIssue>(),
            new List<Piece>());

        var top = new List<(BlockDef Block, List<Span> Spans, bool IsException)>();
        foreach (var block in schedule.Blocks.Where(block => block.Within is null))
        {
            if (!Applies(block, context) || context.Skip.Contains(block.Id) || IsHolidaySkipped(block, block.SkipHoliday, context))
            {
                continue;
            }

            top.Add((block, SpansOf(block, date, context), IsException: false));
        }

        foreach (var block in exception?.Blocks ?? ImmutableArray<BlockDef>.Empty)
        {
            if (IsHolidaySkipped(block, block.SkipHoliday, context))
            {
                continue;
            }

            top.Add((block, SpansOf(block, date, context), IsException: true));
        }

        for (var i = 0; i < top.Count; i++)
        {
            for (var j = i + 1; j < top.Count; j++)
            {
                if (Overlaps(top[i].Spans, top[j].Spans))
                {
                    context.Issues.Add(new ScheduleIssue(
                        top[j].Block,
                        $"固定予定のブロックが重なっています: {top[i].Block.Id} と {top[j].Block.Id}"));
                }
            }
        }

        foreach (var item in top)
        {
            if (item.IsException)
            {
                AddLeaf(item.Block, item.Spans, context);
            }
            else
            {
                Expand(item.Block, item.Spans, Inherited.Empty, context);
            }
        }

        return BuildResult(definitions, context);
    }

    /// <summary>
    /// 日付に当たる期間の ID を返す。複数当たれば、いちばん短いもの。どれにも当たらなければ "default"。
    /// </summary>
    /// <param name="periods">期間の列。</param>
    /// <param name="date">日付。</param>
    public static string PeriodOf(ImmutableArray<PeriodDef> periods, DateOnly date)
    {
        var monthDay = new MonthDay(date.Month, date.Day);
        PeriodDef? best = null;
        var bestLength = int.MaxValue;
        foreach (var period in periods)
        {
            if (!Contains(period, monthDay))
            {
                continue;
            }

            var length = (period.To.Ordinal - period.From.Ordinal + 366) % 366;
            if (length < bestLength)
            {
                best = period;
                bestLength = length;
            }
        }

        return best?.Id ?? "default";
    }

    private static bool Contains(PeriodDef period, MonthDay monthDay) =>
        period.From.CompareTo(period.To) <= 0
            ? period.From.CompareTo(monthDay) <= 0 && monthDay.CompareTo(period.To) <= 0
            : period.From.CompareTo(monthDay) <= 0 || monthDay.CompareTo(period.To) <= 0;

    private static bool IsHolidaySkipped(BlockDef block, bool? skipHoliday, DayContext context) =>
        (skipHoliday ?? false) && context.Holiday;

    private static bool Applies(BlockDef block, DayContext context) =>
        (block.In.IsEmpty || block.In.Contains(context.Period))
        && (block.Days.IsEmpty || block.Days.Contains(context.Date.DayOfWeek));

    private static void Expand(BlockDef block, IReadOnlyList<Span> spans, Inherited inherited, DayContext context)
    {
        var effective = new Inherited(
            block.Action ?? inherited.Action,
            block.Place ?? inherited.Place,
            block.Phone ?? inherited.Phone,
            block.PhoneEvery ?? inherited.PhoneEvery,
            block.SkipHoliday ?? inherited.SkipHoliday);

        var accepted = new List<(BlockDef Child, List<Span> Spans)>();
        foreach (var child in context.Blocks.Where(candidate => candidate.Within == block.Id))
        {
            if (context.Skip.Contains(child.Id) || !Applies(child, context) || IsHolidaySkipped(child, effective.SkipHoliday, context))
            {
                continue;
            }

            var fitted = new List<Span>();
            var outside = false;
            foreach (var span in SpansOf(child, context.Date, context))
            {
                var inside = Fit(spans, span);
                if (inside is null)
                {
                    outside = true;
                    break;
                }

                fitted.Add(inside.Value);
            }

            if (outside)
            {
                context.Issues.Add(new ScheduleIssue(child, $"子のブロックが親「{block.Id}」の時間からはみ出しています: {child.Id}"));
                continue;
            }

            accepted.Add((child, fitted));
        }

        for (var i = 0; i < accepted.Count; i++)
        {
            for (var j = i + 1; j < accepted.Count; j++)
            {
                if (Overlaps(accepted[i].Spans, accepted[j].Spans))
                {
                    context.Issues.Add(new ScheduleIssue(
                        accepted[j].Child,
                        $"同じ親の中で子のブロックが重なっています: {accepted[i].Child.Id} と {accepted[j].Child.Id}"));
                }
            }
        }

        foreach (var (child, childSpans) in accepted)
        {
            Expand(child, childSpans, effective, context);
        }

        var childSpansAll = accepted.SelectMany(item => item.Spans).ToList();
        foreach (var span in spans)
        {
            foreach (var remaining in Subtract(span, childSpansAll))
            {
                context.Pieces.Add(new Piece(block, remaining.Start, remaining.End, effective));
            }
        }
    }

    /// <summary>
    /// 例外日に足したブロックは入れ子を持たない。同じ ID の基本のブロックの子を引き込まないよう、子を探さずに区間を並べる。
    /// </summary>
    private static void AddLeaf(BlockDef block, IReadOnlyList<Span> spans, DayContext context)
    {
        var inherited = new Inherited(block.Action, block.Place, block.Phone, block.PhoneEvery, block.SkipHoliday);
        foreach (var span in spans)
        {
            context.Pieces.Add(new Piece(block, span.Start, span.End, inherited));
        }
    }

    private static ResolvedDay BuildResult(Definitions.Definitions definitions, DayContext context)
    {
        var counts = new Dictionary<string, int>();
        var blocks = ImmutableArray.CreateBuilder<ResolvedBlock>();
        foreach (var piece in context.Pieces.OrderBy(item => item.Start).ThenBy(item => item.Block.Id, StringComparer.Ordinal))
        {
            var defId = piece.Block.Id;
            counts.TryGetValue(defId, out var seen);
            counts[defId] = seen + 1;
            var suffix = seen == 0 ? string.Empty : $".{seen + 1}";
            var id = $"{defId}{suffix}@{context.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

            var action = piece.Inherited.Action;
            if (action is null)
            {
                context.Issues.Add(new ScheduleIssue(piece.Block, $"行動が決まらないブロックです: {defId}"));
                continue;
            }

            var phone = piece.Inherited.Phone
                ?? (definitions.Actions.TryGetValue(action, out var actionDef) ? actionDef.Phone : PhonePolicy.Closed);
            blocks.Add(new ResolvedBlock(
                id,
                defId,
                context.Date,
                piece.Start,
                piece.End,
                action,
                piece.Inherited.Place,
                phone,
                piece.Inherited.PhoneEvery,
                piece.Block.With,
                piece.Block.OnPartnerAbsent ?? PartnerAbsentPolicy.Continue));
        }

        return new ResolvedDay(
            context.Date,
            blocks.ToImmutable(),
            context.Issues.ToImmutableArray());
    }

    /// <summary>
    /// ブロックの時間の範囲を、分の区間にする。同じブロックの範囲どうしが重なれば問題として記録する。
    /// </summary>
    private static List<Span> SpansOf(BlockDef block, DateOnly date, DayContext context)
    {
        var spans = new List<Span>();
        foreach (var range in block.Ranges)
        {
            var start = range.Start.Hour * 60 + range.Start.Minute;
            var end = range.End.Hour * 60 + range.End.Minute;
            if (end <= start)
            {
                end += MinutesPerDay;
            }

            spans.Add(new Span(start, end));
        }

        for (var i = 0; i < spans.Count; i++)
        {
            for (var j = i + 1; j < spans.Count; j++)
            {
                if (Overlaps([spans[i]], [spans[j]]))
                {
                    context.Issues.Add(new ScheduleIssue(block, $"同じブロックの時間帯が重なっています: {block.Id}"));
                }
            }
        }

        return spans;
    }

    private static Span? Fit(IReadOnlyList<Span> parents, Span child)
    {
        foreach (var parent in parents)
        {
            foreach (var shift in new[] { 0, MinutesPerDay, -MinutesPerDay })
            {
                var start = child.Start + shift;
                var end = child.End + shift;
                if (parent.Start <= start && end <= parent.End)
                {
                    return new Span(start, end);
                }
            }
        }

        return null;
    }

    private static bool Overlaps(IReadOnlyList<Span> left, IReadOnlyList<Span> right) =>
        left.Any(a => right.Any(b => a.Start < b.End && b.Start < a.End));

    private static List<Span> Subtract(Span span, IReadOnlyList<Span> cuts)
    {
        var result = new List<Span>();
        var cursor = span.Start;
        foreach (var cut in cuts.Where(cut => cut.End > span.Start && cut.Start < span.End).OrderBy(cut => cut.Start))
        {
            if (cut.Start > cursor)
            {
                result.Add(new Span(cursor, Math.Min(cut.Start, span.End)));
            }

            cursor = Math.Max(cursor, cut.End);
        }

        if (cursor < span.End)
        {
            result.Add(new Span(cursor, span.End));
        }

        return result;
    }

    private readonly record struct Span(int Start, int End);

    private readonly record struct Inherited(
        string? Action,
        string? Place,
        PhonePolicy? Phone,
        TimeSpan? PhoneEvery,
        bool? SkipHoliday)
    {
        public static Inherited Empty { get; } = new(null, null, null, null, null);
    }

    private readonly record struct Piece(BlockDef Block, int Start, int End, Inherited Inherited);

    private sealed record DayContext(
        Definitions.Definitions Definitions,
        DateOnly Date,
        string Period,
        bool Holiday,
        ImmutableHashSet<string> Skip,
        ImmutableArray<BlockDef> Blocks,
        List<ScheduleIssue> Issues,
        List<Piece> Pieces);
}
