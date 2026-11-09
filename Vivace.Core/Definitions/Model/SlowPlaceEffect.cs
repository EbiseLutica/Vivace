namespace Vivace.Core.Definitions;

/// <summary>移動時間を増やす効果。場所 ID か場所のタグで決める。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="PlaceTag">場所のタグ。</param>
/// <param name="Add">増やす時間。</param>
/// <param name="Factor">掛ける倍率。</param>
public sealed record SlowPlaceEffect(string? Place, string? PlaceTag, TimeSpan? Add, double? Factor);
