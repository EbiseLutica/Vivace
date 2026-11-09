namespace Vivace.Core.Domain;

/// <summary>状態変数の1本。基準時刻での値と、いまの率から、値 + 率 × 経過時間で読む（D5 5.6）。</summary>
/// <param name="BaseTime">基準の時刻。</param>
/// <param name="BaseValue">基準の値。</param>
/// <param name="Rate">いまの率（1時間あたり）。</param>
public readonly record struct VitalTrack(DateTimeOffset BaseTime, double BaseValue, double Rate);
