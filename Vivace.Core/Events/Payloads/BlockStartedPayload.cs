namespace Vivace.Core.Events.Payloads;

/// <summary>ブロックの開始。固定・計画のブロックは ID を持ち、自由時間では ID が null（決めどきを積む）。</summary>
/// <param name="BlockId">ブロックの ID（定義 ID@開始日）。自由時間は null。</param>
/// <param name="PlanVersion">プランの版。</param>
public sealed record BlockStartedPayload(string? BlockId, long PlanVersion) : EventPayload(1);
