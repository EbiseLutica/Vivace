namespace Vivace.Core.Time;

/// <summary>現地時刻と UTC の相互変換。時差は固定。</summary>
public static class LocalClock
{
    /// <summary>UTC の時刻を、現地の日付と時刻にする。</summary>
    /// <param name="utc">UTC の時刻。</param>
    /// <param name="utcOffset">ワールドの時差。</param>
    public static (DateOnly Date, TimeOnly Time) ToLocal(DateTimeOffset utc, TimeSpan utcOffset)
    {
        var local = utc.ToUniversalTime().ToOffset(utcOffset);
        return (DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime));
    }

    /// <summary>現地の日付と時刻を、UTC の時刻にする。</summary>
    /// <param name="date">現地の日付。</param>
    /// <param name="time">現地の時刻。</param>
    /// <param name="utcOffset">ワールドの時差。</param>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeSpan utcOffset) =>
        new DateTimeOffset(date.ToDateTime(time), utcOffset).ToUniversalTime();
}
