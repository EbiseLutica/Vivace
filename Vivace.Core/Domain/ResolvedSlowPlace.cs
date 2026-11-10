namespace Vivace.Core.Domain;

/// <summary>解決済みの移動時間の効果（対象の場所を決めたあと）。</summary>
/// <param name="Place">対象の場所。</param>
/// <param name="Add">増やす時間。</param>
/// <param name="Factor">掛ける倍率。</param>
public sealed record ResolvedSlowPlace(PlaceId Place, TimeSpan? Add, double? Factor);
