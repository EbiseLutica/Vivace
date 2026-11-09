using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>体の反応（段 4）。しきい値を越えた。</summary>
/// <param name="Vital">状態変数。</param>
/// <param name="Threshold">しきい値。</param>
/// <param name="StateVersion">状態変数の版。</param>
public sealed record VitalThresholdPayload(VitalName Vital, double Threshold, long StateVersion) : EventPayload(1);
