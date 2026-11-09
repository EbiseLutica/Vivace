using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>例外日（D3 3.5）。</summary>
/// <param name="Date">日付。</param>
/// <param name="Skip">この日は外すブロックの定義 ID。</param>
/// <param name="Blocks">この日だけ足すブロック。</param>
/// <param name="Source">出どころ。</param>
public sealed record ExceptionDef(DateOnly Date, ImmutableArray<string> Skip, ImmutableArray<BlockDef> Blocks, DefinitionSource Source);
