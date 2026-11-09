namespace Vivace.Core.Definitions;

/// <summary>現地の時刻の範囲。End が Start より小さければ日をまたぐ。</summary>
/// <param name="Start">開始。</param>
/// <param name="End">終了。</param>
public readonly record struct TimeRange(TimeOnly Start, TimeOnly End);
