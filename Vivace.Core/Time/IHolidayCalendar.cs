namespace Vivace.Core.Time;

/// <summary>
/// 祝日の判定。提供元はワールドのカレンダーが決める。コアはインターフェースだけを持つ。
/// </summary>
public interface IHolidayCalendar
{
    /// <summary>その日が祝日かどうか。</summary>
    /// <param name="date">現地の日付。</param>
    bool IsHoliday(DateOnly date);
}
