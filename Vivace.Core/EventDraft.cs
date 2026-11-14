using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core;

/// <summary>キューに入る前のイベントのドラフト。Apply の後続、決める側の出力、投入のいずれにも使う。</summary>
/// <param name="Time">時刻。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="WorldId">ワールド。</param>
/// <param name="PersonaId">ペルソナ。</param>
/// <param name="Payload">中身。</param>
/// <param name="Guard">適用前の検査（決める側の出力のとき）。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（決める側の出力のとき）。</param>
/// <param name="Origin">出所。null なら契機の出所を引き継ぐ。</param>
public sealed record EventDraft(
    DateTimeOffset Time,
    string EventType,
    WorldId? WorldId,
    PersonaId? PersonaId,
    EventPayload Payload,
    Guard? Guard = null,
    long? BatchSeq = null,
    Origin? Origin = null)
{
    /// <summary>ペルソナの後続のイベントを作る。</summary>
    /// <param name="time">時刻。</param>
    /// <param name="eventType">イベントタイプの名前。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="payload">中身。</param>
    internal static EventDraft Next(DateTimeOffset time, string eventType, PersonaState persona, EventPayload payload) =>
        new(time, eventType, persona.World, persona.Id, payload);

    /// <summary>決めどきのイベントを作る。</summary>
    /// <param name="now">時刻。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="reason">決めどきの理由。</param>
    /// <param name="blockId">関わるブロックの ID。なければ null。</param>
    internal static EventDraft DecisionRequested(DateTimeOffset now, PersonaState persona, DecisionReason reason, string? blockId) =>
        Next(now, EventTypes.DecisionRequested, persona, new DecisionRequestedPayload(reason, blockId));

    /// <summary>行動の開始のイベントを作る。</summary>
    /// <param name="now">時刻。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="actionId">行動の定義 ID。</param>
    /// <param name="place">場所。</param>
    /// <param name="duration">所要時間。</param>
    /// <param name="reason">行動の理由。</param>
    /// <param name="blockId">ブロックの ID。なければ null。</param>
    internal static EventDraft ActionStarted(DateTimeOffset now, PersonaState persona, string actionId, PlaceId place, TimeSpan duration, ActivityReason reason, string? blockId) =>
        Next(now, EventTypes.ActionStarted, persona, new ActionStartedPayload(actionId, place, duration, reason, persona.ActivityVersion, blockId));

    /// <summary>移動の開始のイベントを作る。</summary>
    /// <param name="now">時刻。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="from">出発地。</param>
    /// <param name="to">目的地。</param>
    /// <param name="route">経路。</param>
    /// <param name="arrival">到着の時刻。</param>
    /// <param name="next">到着のあとに始める行動。なければ null。</param>
    internal static EventDraft TravelStarted(DateTimeOffset now, PersonaState persona, PlaceId from, PlaceId to, ImmutableArray<RouteStep> route, DateTimeOffset arrival, PendingAction? next) =>
        Next(now, EventTypes.TravelStarted, persona, new TravelStartedPayload(from, to, route, arrival, persona.ActivityVersion, next));
}
