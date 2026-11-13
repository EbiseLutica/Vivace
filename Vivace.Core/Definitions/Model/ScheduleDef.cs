using System.Collections.Immutable;

namespace Vivace.Core.Definitions.Model;

/// <summary>固定予定。</summary>
/// <param name="Periods">期間。</param>
/// <param name="Blocks">ブロック。</param>
/// <param name="Exceptions">例外日。</param>
public sealed record ScheduleDef(
    ImmutableArray<PeriodDef> Periods,
    ImmutableArray<BlockDef> Blocks,
    ImmutableArray<ExceptionDef> Exceptions);
