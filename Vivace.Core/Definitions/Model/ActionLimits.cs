namespace Vivace.Core.Definitions;

/// <summary>行動の制限。</summary>
/// <param name="Cooldown">前回の開始から、この時間が経つまで候補にしない。</param>
/// <param name="MaxPerDay">現地の1日に、この回数まで。</param>
/// <param name="Hours">現地の時刻がこの範囲にあるときだけ。</param>
public sealed record ActionLimits(TimeSpan? Cooldown, int? MaxPerDay, TimeRange? Hours);
