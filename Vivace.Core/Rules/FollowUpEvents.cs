using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Routes;

namespace Vivace.Core.Rules;

/// <summary>ルールが後続のイベントを作る。</summary>
internal static class FollowUpEvents
{
    /// <summary>ペルソナの後続のイベントを作る。</summary>
    internal static NewEvent Next(DateTimeOffset time, string eventType, PersonaState persona, EventPayload payload) =>
        new(time, eventType, persona.World, persona.Id, payload);

    /// <summary>決めどきのイベントを作る。</summary>
    internal static NewEvent DecisionRequested(DateTimeOffset now, PersonaState persona, DecisionReason reason, string? blockId) =>
        Next(now, EventTypes.DecisionRequested, persona, new DecisionRequestedPayload(reason, blockId));

    /// <summary>行動の開始のイベントを作る。</summary>
    internal static NewEvent ActionStarted(DateTimeOffset now, PersonaState persona, string actionId, PlaceId place, TimeSpan duration, ActivityReason reason, string? blockId) =>
        Next(now, EventTypes.ActionStarted, persona, new ActionStartedPayload(actionId, place, duration, reason, persona.ActivityVersion, blockId));

    /// <summary>移動の開始のイベントを作る。</summary>
    internal static NewEvent TravelStarted(DateTimeOffset now, PersonaState persona, PlaceId from, PlaceId to, ImmutableArray<RouteStep> route, DateTimeOffset arrival, PendingAction? next) =>
        Next(now, EventTypes.TravelStarted, persona, new TravelStartedPayload(from, to, route, arrival, persona.ActivityVersion, next));
}
