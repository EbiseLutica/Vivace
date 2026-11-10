using System.Globalization;

namespace Vivace.Core.Time;

/// <summary>
/// 年を指定しない月日（"MM-DD"）。年をまたぐ期間の端に使う。
/// </summary>
/// <param name="Month">月（1〜12）。</param>
/// <param name="Day">日。</param>
public readonly record struct MonthDay(int Month, int Day) : IComparable<MonthDay>
{
    /// <summary>月日を、うるう年の同じ日の通し番号にする（比較のため）。</summary>
    public int Ordinal => new DateOnly(2000, Month, Day).DayOfYear;

    /// <summary>月日の順序で比べる。</summary>
    /// <param name="other">比べる相手。</param>
    public int CompareTo(MonthDay other) => Ordinal.CompareTo(other.Ordinal);

    /// <summary>"MM-DD" の形にする。</summary>
    public override string ToString() => $"{Month.ToString("00", CultureInfo.InvariantCulture)}-{Day.ToString("00", CultureInfo.InvariantCulture)}";
}
