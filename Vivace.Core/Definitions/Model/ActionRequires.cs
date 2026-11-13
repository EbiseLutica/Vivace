using System.Collections.Immutable;

namespace Vivace.Core.Definitions.Model;

/// <summary>行動の実行の条件。</summary>
/// <param name="PlaceTags">場所が持つべきタグ。</param>
/// <param name="Items">ペルソナが持つべき携行品のタグ。</param>
/// <param name="ChannelCapability">つながるべきチャンネルの能力。</param>
public sealed record ActionRequires(ImmutableArray<string> PlaceTags, ImmutableArray<string> Items, string? ChannelCapability);
