namespace Vivace.Core.Events.Guards;

/// <summary>プランのバージョンが、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときのプランのバージョン。</param>
public sealed record PlanVersionCheck(long Expected) : GuardCheck;
