using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Routes;

namespace Vivace.Core.Rules;

/// <summary>後続のイベント。Apply と同じトランザクションでキューに入る。</summary>
/// <param name="Time">時刻。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="World">ワールド。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Payload">中身。</param>
/// <param name="Guard">適用前の検査（決める側の出力のとき）。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（決める側の出力のとき）。</param>
/// <param name="Origin">出所。null なら契機の出所を引き継ぐ。</param>
public sealed record NewEvent(
    DateTimeOffset Time,
    string EventType,
    WorldId? World,
    PersonaId? Persona,
    EventPayload Payload,
    Guard? Guard = null,
    long? BatchSeq = null,
    Origin? Origin = null)
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
