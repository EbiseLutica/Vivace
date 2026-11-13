namespace Vivace.Core.Domain.State;

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
    long Version)
{
    /// <summary>状態変数の1本を取り出す。</summary>
    /// <param name="vital">取り出す変数。</param>
    public VitalTrack Get(VitalName vital) => vital switch
    {
        VitalName.Energy => Energy,
        VitalName.Health => Health,
        VitalName.Boredom => Boredom,
        _ => Social,
    };

    /// <summary>状態変数の1本を差し替えた集まりを返す。版は変えない。</summary>
    /// <param name="vital">差し替える変数。</param>
    /// <param name="value">新しい値。</param>
    public Vitals With(VitalName vital, VitalTrack value) => vital switch
    {
        VitalName.Energy => this with { Energy = value },
        VitalName.Health => this with { Health = value },
        VitalName.Boredom => this with { Boredom = value },
        _ => this with { Social = value },
    };
}
