using Vivace.Core.Domain;

namespace Vivace.Core.Definitions;

/// <summary>変換の辞書の初期値の1件。</summary>
/// <param name="From">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="To">変換先（convert のとき）。</param>
/// <param name="Source">出どころ。</param>
public sealed record TermDef(string From, TermVerdict Verdict, string? To, DefinitionSource Source);
