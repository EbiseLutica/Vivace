using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>移動の到着。</summary>
/// <param name="Place">着いた場所。</param>
/// <param name="ActivityVersion">行動の版。古ければ捨てる。</param>
public sealed record TravelArrivedPayload(PlaceId Place, long ActivityVersion) : EventPayload(1);
