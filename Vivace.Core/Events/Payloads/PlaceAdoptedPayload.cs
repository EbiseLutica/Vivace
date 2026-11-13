using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>場所の採用。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record PlaceAdoptedPayload(PlaceId Place) : EventPayload(1);
