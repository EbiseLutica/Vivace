namespace Vivace.Core.Definitions.Model;

/// <summary>場所を閉鎖する効果。場所 ID か場所のタグで決める。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="PlaceTag">場所のタグ。</param>
public sealed record ClosePlaceEffect(string? Place, string? PlaceTag);
