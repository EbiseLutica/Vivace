using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>固定予定（D3 3.5）。</summary>
/// <param name="Periods">期間。</param>
/// <param name="Blocks">ブロック。</param>
/// <param name="Exceptions">例外日。</param>
public sealed record ScheduleDef(
    ImmutableArray<PeriodDef> Periods,
    ImmutableArray<BlockDef> Blocks,
    ImmutableArray<ExceptionDef> Exceptions);
