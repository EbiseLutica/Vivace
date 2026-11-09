namespace Vivace.Core.Domain;

/// <summary>経路の1辺（出発と到着の場所、移動時間（分））。移動の経路は、この辺の列として確定させる（D5 5.10）。</summary>
/// <param name="From">出発の場所。</param>
/// <param name="To">到着の場所。</param>
/// <param name="Minutes">移動時間（分）。正の整数。</param>
public readonly record struct RouteStep(PlaceId From, PlaceId To, int Minutes);
