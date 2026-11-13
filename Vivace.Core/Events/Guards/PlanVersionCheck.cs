namespace Vivace.Core.Events.Guards;

/// <summary>プランの版が、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときのプランの版。</param>
public sealed record PlanVersionCheck(long Expected) : GuardCheck;
