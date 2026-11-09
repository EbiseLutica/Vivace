using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>場所の廃止（段 3）。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record PlaceRetiredPayload(PlaceId Place) : EventPayload(1);
