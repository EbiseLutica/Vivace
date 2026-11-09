using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>行動の決定（Decider の出力）。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動する場所。</param>
/// <param name="Route">辺の列。移動がなければ空。</param>
/// <param name="TravelMinutes">移動時間（分）。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="IsSubAction">副行動かどうか（スマホを開く、投稿する）。</param>
/// <param name="Draws">所要時間などに乱数を引いた範囲。</param>
public sealed record ActivityChosenPayload(
    string ActionId,
    PlaceId Place,
    ImmutableArray<RouteStep> Route,
    int TravelMinutes,
    TimeSpan Duration,
    bool IsSubAction,
    RngDraws? Draws) : EventPayload(1);
