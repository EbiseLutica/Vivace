using Vivace.Core.Domain.State;


namespace Vivace.Core.Events.Payloads;

/// <summary>プランの確定。Planner の出力を適用したもの。</summary>
/// <param name="Plan">確定したプラン。</param>
public sealed record PlanCommittedPayload(DayPlan Plan) : EventPayload(1);
