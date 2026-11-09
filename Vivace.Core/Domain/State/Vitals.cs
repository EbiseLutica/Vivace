namespace Vivace.Core.Domain;

/// <summary>4つの状態変数と、その版。</summary>
/// <param name="Energy">気力。</param>
/// <param name="Health">健康度。</param>
/// <param name="Boredom">退屈度。</param>
/// <param name="Social">社交欲求。</param>
/// <param name="Version">状態変数の版。率が変わるたびに上がる。</param>
public sealed record Vitals(
    VitalTrack Energy,
    VitalTrack Health,
    VitalTrack Boredom,
    VitalTrack Social,
    long Version);
