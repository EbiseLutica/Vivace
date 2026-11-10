namespace Vivace.Core.Domain;

/// <summary>状態変数の1本の変化。基準時刻での値と率から、値 + 率 × 経過時間で読む。</summary>
/// <param name="BaseTime">基準の時刻。</param>
/// <param name="BaseValue">基準の値。</param>
/// <param name="Rate">いまの率（1時間あたり）。</param>
public readonly record struct VitalTrack(DateTimeOffset BaseTime, double BaseValue, double Rate)
{
    /// <summary>範囲に切り詰める。</summary>
    /// <param name="value">値。</param>
    /// <param name="range">範囲。</param>
    public static double Clamp(double value, (double Min, double Max) range) =>
        value < range.Min ? range.Min : value > range.Max ? range.Max : value;

    /// <summary>時刻 now の値を読む。範囲の中に切り詰める。</summary>
    /// <param name="now">読む時刻。</param>
    /// <param name="range">範囲。</param>
    public double ValueAt(DateTimeOffset now, (double Min, double Max) range) =>
        Clamp(BaseValue + Rate * (now - BaseTime).TotalHours, range);
}
