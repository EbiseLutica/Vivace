using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>場所の廃止。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record PlaceRetiredPayload(PlaceId Place) : EventPayload(1);
