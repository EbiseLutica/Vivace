namespace Vivace.Core.Time;

/// <summary>
/// 祝日の判定。提供元はワールドの暦が決める（P-18）。コアは口だけを持つ。
/// </summary>
public interface IHolidayCalendar
{
    /// <summary>その日が祝日かどうか。</summary>
    /// <param name="date">現地の日付。</param>
    bool IsHoliday(DateOnly date);
}

/// <summary>祝日のない暦。ワールドが暦を持たないとき（P-18 の既定）に使う。</summary>
public sealed class NoHolidays : IHolidayCalendar
{
    /// <summary>共有の1つ。</summary>
    public static NoHolidays Instance { get; } = new();

    /// <summary>いつも祝日ではない。</summary>
    /// <param name="date">現地の日付。</param>
    public bool IsHoliday(DateOnly date) => false;
}
