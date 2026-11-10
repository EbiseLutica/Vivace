using System.Collections.Immutable;

namespace Vivace.Core.Schedule;

/// <summary>1日の固定予定の結果。</summary>
/// <param name="Date">日付。</param>
/// <param name="Blocks">開始が今日のブロック。開始の順。</param>
/// <param name="Issues">重なりや、親からのはみ出し。</param>
public sealed record ResolvedDay(DateOnly Date, ImmutableArray<ResolvedBlock> Blocks, ImmutableArray<ScheduleIssue> Issues);
