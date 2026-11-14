namespace Vivace.Core.Events.Payloads;

/// <summary>プランの作り直しの時刻。</summary>
/// <param name="PlanVersion">プランのバージョン。古ければ捨てる。</param>
public sealed record PlanDuePayload(long PlanVersion) : EventPayload(1);
