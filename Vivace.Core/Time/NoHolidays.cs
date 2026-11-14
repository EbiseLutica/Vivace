namespace Vivace.Core.Time;

/// <summary>祝日のないカレンダー。ワールドがカレンダーを持たないときに使う。</summary>
public sealed class NoHolidays : IHolidayCalendar
{
    /// <summary>共有の1つ。</summary>
    public static NoHolidays Instance { get; } = new();

    /// <summary>いつも祝日ではない。</summary>
    /// <param name="date">現地の日付。</param>
    public bool IsHoliday(DateOnly date) => false;
}
