using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Tests.Serialization;

/// <summary>イベントタイプごとの、手で作った小さなペイロードの例。</summary>
public static class PayloadSamples
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);

    private static readonly PlaceId Home = new("home");

    private static readonly PlaceId Station = new("station");

    private static readonly RngDraws Draws = new("taro", "decide", 1, 2);

    /// <summary>イベントタイプの名前から、例のペイロードを引く（全イベントタイプ分）。</summary>
    public static IReadOnlyDictionary<string, EventPayload> ByEventType { get; } = Build();

    /// <summary>JSON の文字列を、要素として読む。</summary>
    /// <param name="text">JSON の文字列。</param>
    public static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static Dictionary<string, EventPayload> Build()
    {
        var plan = new DayPlan(
            2,
            T0,
            T0.AddDays(1),
            T0.AddHours(6),
            PlanSource.Llm,
            [new PlanBlock("b1", BlockKind.Fixed, T0, T0.AddHours(1), "sleep", Home, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            "寝る");
        var item = new InboxItem(
            7,
            "misskey",
            InboxKind.Message,
            "misskey",
            new Sender("u1", "花子", "hanako"),
            Audience.Public,
            "conv-1",
            "こんにちは",
            [new Attachment("image", "https://example.invalid/a.png", null)],
            [new ThreadMessage(new Sender("u2", "太郎", null), true, "やあ", T0, [])],
            null,
            "note-1",
            T0,
            Json("{\"id\":\"n1\"}"));
        var effects = new ResolvedIncidentEffects(
            new ResolvedSlowPlace(Station, TimeSpan.FromMinutes(5), 1.5),
            Home,
            ImmutableDictionary<VitalName, double>.Empty.Add(VitalName.Energy, -2.5),
            "wet");

        return new Dictionary<string, EventPayload>(StringComparer.Ordinal)
        {
            [EventTypes.DayStarted] = new DayStartedPayload(new DateOnly(2026, 11, 1)),
            [EventTypes.ActionEnded] = new ActionEndedPayload("read", 3),
            [EventTypes.TravelArrived] = new TravelArrivedPayload(Home, 4),
            [EventTypes.IncidentResolved] = new IncidentResolvedPayload(12),
            [EventTypes.IncidentOccurred] = new IncidentOccurredPayload("rain", new PersonaId("taro"), TimeSpan.FromMinutes(30), effects),
            [EventTypes.PlaceAdded] = new PlaceAddedPayload(new PlaceId("cafe"), "喫茶店", ["food"], "town", [new PlaceLink(Station, 5)], PlaceStatus.Candidate, Draws),
            [EventTypes.PlaceAdopted] = new PlaceAdoptedPayload(new PlaceId("cafe")),
            [EventTypes.PlaceRetired] = new PlaceRetiredPayload(new PlaceId("cafe")),
            [EventTypes.TermRecorded] = new TermRecordedPayload("まじ", TermVerdict.Convert, "本当", "slang"),
            [EventTypes.VitalAdjusted] = new VitalAdjustedPayload(VitalName.Social, VitalAdjustedPayload.VitalAdjustKind.Add, 1.5, "test"),
            [EventTypes.ConditionAttached] = new ConditionAttachedPayload("sleepy", TimeSpan.FromHours(2)),
            [EventTypes.ConditionDetached] = new ConditionDetachedPayload("sleepy"),
            [EventTypes.PersonaCreated] = new PersonaCreatedPayload(new PersonaDefId("taro"), new PlaceId("taro_room")),
            [EventTypes.PersonaDestroyed] = new PersonaDestroyedPayload(),
            [EventTypes.PersonaGap] = new PersonaGapPayload(T0, T0.AddDays(1)),
            [EventTypes.WorldCreated] = new WorldCreatedPayload(new WorldDefId("testville"), 20261101UL),
            [EventTypes.WorldDestroyed] = new WorldDestroyedPayload(),
            [EventTypes.DefinitionsReloaded] = new DefinitionsReloadedPayload("abc"),
            [EventTypes.VitalThreshold] = new VitalThresholdPayload(VitalName.Energy, 20, 5),
            [EventTypes.NotificationArrived] = new NotificationArrivedPayload(item),
            [EventTypes.OutboundSent] = new OutboundSentPayload(3, "ext1"),
            [EventTypes.OutboundFailed] = new OutboundFailedPayload(3, "timeout", T0),
            [EventTypes.OutboundUnknown] = new OutboundUnknownPayload(3, "crash"),
            [EventTypes.PlanCommitted] = new PlanCommittedPayload(plan),
            [EventTypes.PlanDue] = new PlanDuePayload(2),
            [EventTypes.BlockStarted] = new BlockStartedPayload("b1", 2),
            [EventTypes.PhoneOpportunity] = new PhoneOpportunityPayload("b1"),
            [EventTypes.DecisionRequested] = new DecisionRequestedPayload(DecisionReason.Free, "b1"),
            [EventTypes.ActivityChosen] = new ActivityChosenPayload("read", Home, [new RouteStep(Home, Station, 5)], 5, TimeSpan.FromMinutes(30), false, Draws),
            [EventTypes.BlockResponded] = new BlockRespondedPayload("b1", BlockRespondedPayload.DisruptionChoice.Late, null),
            [EventTypes.TravelStarted] = new TravelStartedPayload(Home, Station, [new RouteStep(Home, Station, 5)], T0.AddMinutes(5), 4, new PendingAction("read", Station, TimeSpan.FromMinutes(10), ActivityReason.Block, "b1")),
            [EventTypes.ActionStarted] = new ActionStartedPayload("read", Station, TimeSpan.FromMinutes(35), ActivityReason.Chosen, 3, "b1"),
            [EventTypes.OutboundSpeech] = new OutboundSpeechPayload("misskey", OutboundSpeechPayload.OutboundPostKind.Reply, "こんにちは", "note-1", Audience.Public, [7L], Json("{\"replyId\":\"n1\"}"), Draws),
            [EventTypes.OutboundOperation] = new OutboundOperationPayload("misskey", "follow", Json("{\"userId\":\"u1\"}")),
            [EventTypes.InboxHandled] = new InboxHandledPayload([new InboxHandledPayload.InboxOutcome(7, InboxHandledPayload.InboxOutcomeKind.Reply)], null),
            [EventTypes.PostSkipped] = new PostSkippedPayload(PostSkippedPayload.PostSkipReason.Deadline, null),
        };
    }
}
