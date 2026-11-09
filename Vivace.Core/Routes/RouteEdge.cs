using Vivace.Core.Domain;

namespace Vivace.Core.Routes;

/// <summary>
/// 場所の間のつながり（無向の辺と移動時間（分））。
/// </summary>
/// <param name="A">片方の場所。</param>
/// <param name="B">もう片方の場所。</param>
/// <param name="Minutes">移動時間（分）。正の整数。</param>
public readonly record struct RouteEdge(PlaceId A, PlaceId B, int Minutes);
