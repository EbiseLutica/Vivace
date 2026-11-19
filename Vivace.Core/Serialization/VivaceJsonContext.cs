using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Serialization.Converters;

namespace Vivace.Core.Serialization;

/// <summary>
/// 永続化の JSON のソース生成のコンテキスト。ペイロードと、状態の JSON の列に使う型を登録する。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    Converters = new[]
    {
        typeof(PlaceIdConverter),
        typeof(PersonaIdConverter),
        typeof(WorldIdConverter),
        typeof(PersonaDefIdConverter),
        typeof(WorldDefIdConverter),
        typeof(UtcDateTimeOffsetConverter),
    })]
[JsonSerializable(typeof(ActionEndedPayload))]
[JsonSerializable(typeof(ActionStartedPayload))]
[JsonSerializable(typeof(ActivityChosenPayload))]
[JsonSerializable(typeof(BlockRespondedPayload))]
[JsonSerializable(typeof(BlockStartedPayload))]
[JsonSerializable(typeof(ConditionAttachedPayload))]
[JsonSerializable(typeof(ConditionDetachedPayload))]
[JsonSerializable(typeof(DayStartedPayload))]
[JsonSerializable(typeof(DecisionRequestedPayload))]
[JsonSerializable(typeof(DefinitionsReloadedPayload))]
[JsonSerializable(typeof(InboxHandledPayload))]
[JsonSerializable(typeof(IncidentOccurredPayload))]
[JsonSerializable(typeof(IncidentResolvedPayload))]
[JsonSerializable(typeof(NotificationArrivedPayload))]
[JsonSerializable(typeof(OutboundFailedPayload))]
[JsonSerializable(typeof(OutboundOperationPayload))]
[JsonSerializable(typeof(OutboundSentPayload))]
[JsonSerializable(typeof(OutboundSpeechPayload))]
[JsonSerializable(typeof(OutboundUnknownPayload))]
[JsonSerializable(typeof(PersonaCreatedPayload))]
[JsonSerializable(typeof(PersonaDestroyedPayload))]
[JsonSerializable(typeof(PersonaGapPayload))]
[JsonSerializable(typeof(PhoneOpportunityPayload))]
[JsonSerializable(typeof(PlaceAddedPayload))]
[JsonSerializable(typeof(PlaceAdoptedPayload))]
[JsonSerializable(typeof(PlaceRetiredPayload))]
[JsonSerializable(typeof(PlanCommittedPayload))]
[JsonSerializable(typeof(PlanDuePayload))]
[JsonSerializable(typeof(PostSkippedPayload))]
[JsonSerializable(typeof(TermRecordedPayload))]
[JsonSerializable(typeof(TravelArrivedPayload))]
[JsonSerializable(typeof(TravelStartedPayload))]
[JsonSerializable(typeof(VitalAdjustedPayload))]
[JsonSerializable(typeof(VitalThresholdPayload))]
[JsonSerializable(typeof(WorldCreatedPayload))]
[JsonSerializable(typeof(WorldDestroyedPayload))]
[JsonSerializable(typeof(Location))]
[JsonSerializable(typeof(Activity))]
[JsonSerializable(typeof(DayPlan))]
[JsonSerializable(typeof(Vitals))]
[JsonSerializable(typeof(PostingState))]
[JsonSerializable(typeof(ImmutableArray<AttachedCondition>))]
[JsonSerializable(typeof(ImmutableDictionary<string, ActionUsage>))]
[JsonSerializable(typeof(ImmutableDictionary<string, ulong>))]
[JsonSerializable(typeof(ImmutableArray<string>))]
[JsonSerializable(typeof(ImmutableArray<PlaceLink>))]
[JsonSerializable(typeof(ResolvedIncidentEffects))]
[JsonSerializable(typeof(InboxItem))]
[JsonSerializable(typeof(Sender))]
[JsonSerializable(typeof(Origin))]
[JsonSerializable(typeof(Guard))]
[JsonSerializable(typeof(GuardCheck))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonElement?))]
public partial class VivaceJsonContext : JsonSerializerContext
{
}
