namespace Vivace.Core.Time;

/// <summary>時刻をミリ秒の精度に切り捨てる。SQLite の列（Unix ミリ秒）と JSON の時刻が食い違わないように、時計の出す時刻はこの精度にそろえる。</summary>
internal static class Milliseconds
{
    /// <summary>時刻を UTC にして、ミリ秒未満を切り捨てる。</summary>
    /// <param name="time">時刻。</param>
    public static DateTimeOffset Truncate(DateTimeOffset time)
    {
        var ticks = time.UtcTicks;
        return new DateTimeOffset(ticks - ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }
}
