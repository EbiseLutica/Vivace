using System.Globalization;

namespace Vivace.Core.Rules;

/// <summary>計画の期間の鍵を作る。</summary>
internal static class PeriodKeys
{
    /// <summary>期間の鍵（時刻）。記憶の整理を起こすブロックのない期間で使う。</summary>
    internal static string PeriodKeyAt(DateTimeOffset time) => "time:" + time.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
