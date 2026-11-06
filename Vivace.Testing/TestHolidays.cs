using Vivace.Core.Time;

namespace Vivace.Testing;

/// <summary>
/// テスト用の祝日の暦。2026-11-03（文化の日）だけを祝日にする。日本の暦そのものは段階8 で作る（P-18）。
/// </summary>
public sealed class TestHolidays : IHolidayCalendar
{
    private static readonly DateOnly CultureDay = new(2026, 11, 3);

    /// <summary>共有の1つ。</summary>
    public static TestHolidays Instance { get; } = new();

    /// <inheritdoc />
    public bool IsHoliday(DateOnly date) => date == CultureDay;
}
