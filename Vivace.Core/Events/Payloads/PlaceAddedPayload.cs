using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>生えた場所の追加（段 3）。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">タグ。</param>
/// <param name="Group">所属のまとまり。</param>
/// <param name="Links">つながる場所と移動時間（分）。</param>
/// <param name="Status">状態（候補か採用済み）。</param>
/// <param name="Draws">抽選に引いた範囲（候補の選び方に使った場合）。</param>
public sealed record PlaceAddedPayload(
    PlaceId Place,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<PlaceLink> Links,
    PlaceStatus Status,
    RngDraws? Draws) : EventPayload(1);
