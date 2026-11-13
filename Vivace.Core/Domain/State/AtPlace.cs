namespace Vivace.Core.Domain.State;

/// <summary>場所にいる。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record AtPlace(PlaceId Place) : Location;
