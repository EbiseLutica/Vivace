using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>移動の開始。移動中にする。</summary>
/// <param name="From">出発地。</param>
/// <param name="To">目的地。</param>
/// <param name="Route">辺の列。移動がなければ空。</param>
/// <param name="ArrivesAt">到着の時刻。</param>
/// <param name="ActivityVersion">行動の版。</param>
/// <param name="Next">到着のあとに始める行動。</param>
public sealed record TravelStartedPayload(
    PlaceId From,
    PlaceId To,
    ImmutableArray<RouteStep> Route,
    DateTimeOffset ArrivesAt,
    long ActivityVersion,
    PendingAction? Next = null) : EventPayload(1);
