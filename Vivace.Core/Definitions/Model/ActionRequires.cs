using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>行動の実行の条件（D2 2.6）。</summary>
/// <param name="PlaceTags">場所が持つべきタグ。</param>
/// <param name="Items">ペルソナが持つべき携行品のタグ。</param>
/// <param name="ChannelCapability">つながるべきチャンネルの能力。</param>
public sealed record ActionRequires(ImmutableArray<string> PlaceTags, ImmutableArray<string> Items, string? ChannelCapability);
