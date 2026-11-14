using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Schedule;
using Vivace.Core.Time;

namespace Vivace.Core.Planning;

/// <summary>Planner の代わりの出力。固定予定のブロックと、そのあいだの自由時間を並べる。</summary>
public static class FallbackPlanner
{
    /// <summary>次に記憶の整理を起こす行動が見つからないときの、作り直しまでの長さ。</summary>
    public static readonly TimeSpan DefaultRebuildSpan = TimeSpan.FromHours(21);

    /// <summary>プランの範囲の、作り直しのあとの余裕。</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromHours(3);

    /// <summary>
    /// プランを作る。範囲は now から「次に作り直す時刻 + 3時間」まで。
    /// 作り直す時刻は、次に記憶の整理を起こす行動（睡眠など）の開始。なければ now の 21 時間後。
    /// </summary>
    /// <param name="definitions">定義。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="now">トリガーの時刻。</param>
    /// <param name="version">新しいプランのバージョン。</param>
    /// <param name="holidays">祝日のカレンダー。省略すると祝日のないカレンダー。</param>
    public static DayPlan Plan(DefinitionPack definitions, PersonaState persona, DateTimeOffset now, long version, IHolidayCalendar? holidays = null)
    {
        var personaDef = definitions.Personas[persona.DefId];
        var worldDef = definitions.Worlds[personaDef.World];
        var today = LocalTimeHelper.ToLocal(now, worldDef.UtcOffset).Date;
        var fixedBlocks = ResolveAround(definitions, personaDef, worldDef.UtcOffset, today, holidays ?? NoHolidays.Instance);

        var rebuildCandidates = fixedBlocks
            .Where(entry => entry.Start > now && Consolidates(definitions, entry.Block.Action))
            .Select(entry => entry.Start)
            .ToList();
        var rebuildAt = rebuildCandidates.Count > 0 ? rebuildCandidates.Min() : now + DefaultRebuildSpan;
        var horizonEnd = rebuildAt + Margin;

        var blocks = new List<PlanBlock>();
        var cursor = now;
        foreach (var entry in fixedBlocks
            .Where(entry => entry.End > now && entry.Start < horizonEnd)
            .OrderBy(entry => entry.Start)
            .ThenBy(entry => entry.Block.Id, StringComparer.Ordinal))
        {
            if (entry.Start > cursor)
            {
                blocks.Add(FreeBlock(cursor, entry.Start));
            }

            blocks.Add(FixedBlock(entry));
            cursor = entry.End > cursor ? entry.End : cursor;
        }

        if (cursor < horizonEnd)
        {
            blocks.Add(FreeBlock(cursor, horizonEnd));
        }

        return new DayPlan(version, now, horizonEnd, rebuildAt, PlanSource.Fallback, blocks.ToImmutableArray(), null);
    }

    /// <summary>絶対時刻に直した固定予定のブロック。</summary>
    private sealed record Resolved(ResolvedBlock Block, DateTimeOffset Start, DateTimeOffset End);

    /// <summary>前の日から次の日までの固定予定を、絶対時刻に直す。日をまたぐブロックは、開始の日のものとして持つ。</summary>
    private static List<Resolved> ResolveAround(DefinitionPack definitions, PersonaDef personaDef, TimeSpan offset, DateOnly today, IHolidayCalendar holidays)
    {
        var result = new List<Resolved>();
        for (var shift = -1; shift <= 2; shift++)
        {
            var date = today.AddDays(shift);
            var day = FixedScheduleResolver.Resolve(definitions, personaDef, date, holidays);
            var midnight = LocalTimeHelper.ToUtc(date, TimeOnly.MinValue, offset);
            foreach (var block in day.Blocks)
            {
                result.Add(new Resolved(block, midnight.AddMinutes(block.StartMinute), midnight.AddMinutes(block.EndMinute)));
            }
        }

        return result;
    }

    /// <summary>記憶の整理を起こす行動か。</summary>
    private static bool Consolidates(DefinitionPack definitions, string actionId) =>
        definitions.Actions.TryGetValue(actionId, out var action) && action.Effects.Sleep is { Consolidate: true };

    /// <summary>固定予定のブロックを、プランのブロックにする。</summary>
    private static PlanBlock FixedBlock(Resolved entry)
    {
        var block = entry.Block;
        return new PlanBlock(
            block.Id,
            BlockKind.Fixed,
            entry.Start,
            entry.End,
            block.Action,
            block.Place is null ? null : new PlaceId(block.Place),
            block.Phone,
            block.PhoneEvery,
            block.With,
            block.OnPartnerAbsent);
    }

    /// <summary>自由時間のブロックを作る。</summary>
    private static PlanBlock FreeBlock(DateTimeOffset start, DateTimeOffset end) =>
        new(null, BlockKind.Free, start, end, null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue);
}
