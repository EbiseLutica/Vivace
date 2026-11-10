namespace Vivace.Core.Definitions;

/// <summary>状態変数の設定。省略したものは既定値。</summary>
/// <param name="EnergyInitial">気力の初期値。</param>
/// <param name="SocialFloor">社交欲求の下限。</param>
/// <param name="SocialCeiling">社交欲求の上限。</param>
/// <param name="SocialRateAlone">一人のときの社交欲求の率（1時間あたり）。</param>
/// <param name="SocialRateTogether">同席のときの社交欲求の率（1時間あたり）。</param>
/// <param name="SleepTarget">睡眠の目標の長さ。</param>
public sealed record VitalsDef(
    double EnergyInitial,
    double SocialFloor,
    double SocialCeiling,
    double SocialRateAlone,
    double SocialRateTogether,
    TimeSpan SleepTarget);
