using System.Collections.Immutable;

namespace Vivace.Core.Events;

/// <summary>
/// 段。同じ時刻のイベントを並べる順番（D2 2.2）。数が小さいほど先に適用する。
/// </summary>
public enum EventStage
{
    /// <summary>1: 日付が変わる。</summary>
    DayTurn = 1,

    /// <summary>2: 終わることが終わる。</summary>
    Ending = 2,

    /// <summary>3: 世界が変わる。</summary>
    World = 3,

    /// <summary>4: 体が反応する。</summary>
    Reaction = 4,

    /// <summary>5: 外から届く。</summary>
    External = 5,

    /// <summary>6: 予定の時刻になる。</summary>
    Schedule = 6,

    /// <summary>7: 決めて始める。</summary>
    Decision = 7,
}

/// <summary>イベントタイプの1件。名前、段、ペイロードの型。</summary>
/// <param name="Name">イベントタイプの名前（"action.started" など）。</param>
/// <param name="Stage">段。</param>
/// <param name="PayloadType">ペイロードの型。</param>
public sealed record EventTypeInfo(string Name, EventStage Stage, Type PayloadType);

/// <summary>
/// イベントタイプの名前と、段と、ペイロードの型の表（D2 2.2、2.3）。
/// </summary>
public static class EventTypes
{
    /// <summary>1日の始まり。</summary>
    public const string DayStarted = "day.started";

    /// <summary>行動の終了。</summary>
    public const string ActionEnded = "action.ended";

    /// <summary>移動の到着。</summary>
    public const string TravelArrived = "travel.arrived";

    /// <summary>出来事の解消。</summary>
    public const string IncidentResolved = "incident.resolved";

    /// <summary>出来事の発生。</summary>
    public const string IncidentOccurred = "incident.occurred";

    /// <summary>生えた場所の追加。</summary>
    public const string PlaceAdded = "place.added";

    /// <summary>場所の採用。</summary>
    public const string PlaceAdopted = "place.adopted";

    /// <summary>場所の廃止。</summary>
    public const string PlaceRetired = "place.retired";

    /// <summary>変換の辞書への記録。</summary>
    public const string TermRecorded = "term.recorded";

    /// <summary>状態変数の調整（窓口から）。</summary>
    public const string VitalAdjusted = "vital.adjusted";

    /// <summary>状態の付与。</summary>
    public const string ConditionAttached = "condition.attached";

    /// <summary>状態の解除。</summary>
    public const string ConditionDetached = "condition.detached";

    /// <summary>ペルソナの生成。</summary>
    public const string PersonaCreated = "persona.created";

    /// <summary>ペルソナの破棄。</summary>
    public const string PersonaDestroyed = "persona.destroyed";

    /// <summary>追いつきの上限で飛ばした空白の区間。</summary>
    public const string PersonaGap = "persona.gap";

    /// <summary>ワールドの生成。</summary>
    public const string WorldCreated = "world.created";

    /// <summary>ワールドの破棄。</summary>
    public const string WorldDestroyed = "world.destroyed";

    /// <summary>定義の読み直し。</summary>
    public const string DefinitionsReloaded = "definitions.reloaded";

    /// <summary>体の反応（しきい値を越えた）。</summary>
    public const string VitalThreshold = "vital.threshold";

    /// <summary>外からの通知の到着。</summary>
    public const string NotificationArrived = "notification.arrived";

    /// <summary>外向きの操作の送信完了。</summary>
    public const string OutboundSent = "outbound.sent";

    /// <summary>外向きの操作の送信失敗。</summary>
    public const string OutboundFailed = "outbound.failed";

    /// <summary>外向きの操作の送信結果が不明。</summary>
    public const string OutboundUnknown = "outbound.unknown";

    /// <summary>プランの確定。</summary>
    public const string PlanCommitted = "plan.committed";

    /// <summary>プランの作り直しの時刻。</summary>
    public const string PlanDue = "plan.due";

    /// <summary>固定・計画のブロックの開始。</summary>
    public const string BlockStarted = "block.started";

    /// <summary>スマホを開く機会。</summary>
    public const string PhoneOpportunity = "phone.opportunity";

    /// <summary>決めどきの要求。</summary>
    public const string DecisionRequested = "decision.requested";

    /// <summary>行動の決定（Decider の出力）。</summary>
    public const string ActivityChosen = "activity.chosen";

    /// <summary>予定の乱れへの対応の決定。</summary>
    public const string BlockResponded = "block.responded";

    /// <summary>移動の開始。</summary>
    public const string TravelStarted = "travel.started";

    /// <summary>行動の開始。</summary>
    public const string ActionStarted = "action.started";

    /// <summary>言ったこと（発話）の決定。</summary>
    public const string OutboundSpeech = "outbound.speech";

    /// <summary>発話でない外向きの操作の決定。</summary>
    public const string OutboundOperation = "outbound.operation";

    /// <summary>受信箱の捌きの結果。</summary>
    public const string InboxHandled = "inbox.handled";

    /// <summary>投稿の空振り。</summary>
    public const string PostSkipped = "post.skipped";

    private static readonly ImmutableArray<EventTypeInfo> Table = ImmutableArray.Create(
        new EventTypeInfo(DayStarted, EventStage.DayTurn, typeof(DayStartedPayload)),
        new EventTypeInfo(ActionEnded, EventStage.Ending, typeof(ActionEndedPayload)),
        new EventTypeInfo(TravelArrived, EventStage.Ending, typeof(TravelArrivedPayload)),
        new EventTypeInfo(IncidentResolved, EventStage.Ending, typeof(IncidentResolvedPayload)),
        new EventTypeInfo(IncidentOccurred, EventStage.World, typeof(IncidentOccurredPayload)),
        new EventTypeInfo(PlaceAdded, EventStage.World, typeof(PlaceAddedPayload)),
        new EventTypeInfo(PlaceAdopted, EventStage.World, typeof(PlaceAdoptedPayload)),
        new EventTypeInfo(PlaceRetired, EventStage.World, typeof(PlaceRetiredPayload)),
        new EventTypeInfo(TermRecorded, EventStage.World, typeof(TermRecordedPayload)),
        new EventTypeInfo(VitalAdjusted, EventStage.World, typeof(VitalAdjustedPayload)),
        new EventTypeInfo(ConditionAttached, EventStage.World, typeof(ConditionAttachedPayload)),
        new EventTypeInfo(ConditionDetached, EventStage.World, typeof(ConditionDetachedPayload)),
        new EventTypeInfo(PersonaCreated, EventStage.World, typeof(PersonaCreatedPayload)),
        new EventTypeInfo(PersonaDestroyed, EventStage.World, typeof(PersonaDestroyedPayload)),
        new EventTypeInfo(PersonaGap, EventStage.World, typeof(PersonaGapPayload)),
        new EventTypeInfo(WorldCreated, EventStage.World, typeof(WorldCreatedPayload)),
        new EventTypeInfo(WorldDestroyed, EventStage.World, typeof(WorldDestroyedPayload)),
        new EventTypeInfo(DefinitionsReloaded, EventStage.World, typeof(DefinitionsReloadedPayload)),
        new EventTypeInfo(VitalThreshold, EventStage.Reaction, typeof(VitalThresholdPayload)),
        new EventTypeInfo(NotificationArrived, EventStage.External, typeof(NotificationArrivedPayload)),
        new EventTypeInfo(OutboundSent, EventStage.External, typeof(OutboundSentPayload)),
        new EventTypeInfo(OutboundFailed, EventStage.External, typeof(OutboundFailedPayload)),
        new EventTypeInfo(OutboundUnknown, EventStage.External, typeof(OutboundUnknownPayload)),
        new EventTypeInfo(PlanCommitted, EventStage.Schedule, typeof(PlanCommittedPayload)),
        new EventTypeInfo(PlanDue, EventStage.Schedule, typeof(PlanDuePayload)),
        new EventTypeInfo(BlockStarted, EventStage.Schedule, typeof(BlockStartedPayload)),
        new EventTypeInfo(PhoneOpportunity, EventStage.Schedule, typeof(PhoneOpportunityPayload)),
        new EventTypeInfo(DecisionRequested, EventStage.Decision, typeof(DecisionRequestedPayload)),
        new EventTypeInfo(ActivityChosen, EventStage.Decision, typeof(ActivityChosenPayload)),
        new EventTypeInfo(BlockResponded, EventStage.Decision, typeof(BlockRespondedPayload)),
        new EventTypeInfo(TravelStarted, EventStage.Decision, typeof(TravelStartedPayload)),
        new EventTypeInfo(ActionStarted, EventStage.Decision, typeof(ActionStartedPayload)),
        new EventTypeInfo(OutboundSpeech, EventStage.Decision, typeof(OutboundSpeechPayload)),
        new EventTypeInfo(OutboundOperation, EventStage.Decision, typeof(OutboundOperationPayload)),
        new EventTypeInfo(InboxHandled, EventStage.Decision, typeof(InboxHandledPayload)),
        new EventTypeInfo(PostSkipped, EventStage.Decision, typeof(PostSkippedPayload)));

    private static readonly ImmutableDictionary<string, EventTypeInfo> ByName =
        Table.ToImmutableDictionary(info => info.Name);

    /// <summary>すべてのイベントタイプの表。</summary>
    public static ImmutableArray<EventTypeInfo> All => Table;

    /// <summary>イベントタイプの名前から、表の1件を引く。知らない名前なら false。</summary>
    /// <param name="name">イベントタイプの名前。</param>
    /// <param name="info">見つかった1件。</param>
    public static bool TryGet(string name, out EventTypeInfo info) => ByName.TryGetValue(name, out info!);

    /// <summary>イベントタイプの名前から段を引く。知らない名前なら例外。</summary>
    /// <param name="name">イベントタイプの名前。</param>
    public static EventStage StageOf(string name)
    {
        if (!TryGet(name, out var info))
        {
            throw new ArgumentException($"知らないイベントタイプです: {name}", nameof(name));
        }

        return info.Stage;
    }
}
