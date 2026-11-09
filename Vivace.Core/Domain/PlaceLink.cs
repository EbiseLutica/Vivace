namespace Vivace.Core.Domain;

/// <summary>場所のつながり（つながる先と移動時間（分））。ID の順に並べて持つ。</summary>
/// <param name="To">つながる場所。</param>
/// <param name="Minutes">移動時間（分）。正の整数。</param>
public readonly record struct PlaceLink(PlaceId To, int Minutes);
