namespace Vivace.Core.Events;

/// <summary>プランの作り直しの時刻。</summary>
/// <param name="PlanVersion">プランの版。古ければ捨てる。</param>
public sealed record PlanDuePayload(long PlanVersion) : EventPayload(1);
