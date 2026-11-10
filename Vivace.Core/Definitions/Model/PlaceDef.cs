using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>定義の場所。</summary>
/// <param name="Id">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">タグ。</param>
/// <param name="Group">所属のまとまり（家など）。</param>
/// <param name="Links">つながる場所。</param>
/// <param name="Source">出どころ。</param>
public sealed record PlaceDef(
    string Id,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<LinkDef> Links,
    DefinitionSource Source);
