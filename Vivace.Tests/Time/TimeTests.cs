using Vivace.Core.Time;

namespace Vivace.Tests.Time;

/// <summary>時間の文字列、現地時刻の計算、早送りの時計を確かめる。</summary>
public class TimeTests
{
    /// <summary>長さの文字列（"45m"、"1h30m"、"3d"）を読める。</summary>
    [Theory]
    [InlineData("45m", 45)]
    [InlineData("1h30m", 90)]
    [InlineData("2h", 120)]
    [InlineData("1d", 1440)]
    [InlineData("3d", 4320)]
    public void ParsesDurationStrings(string text, int expectedMinutes)
    {
        Assert.True(TimeTextHelper.TryParseDuration(text, out var duration));

        Assert.Equal(expectedMinutes, duration.TotalMinutes);
    }

    /// <summary>単位のない文字列、0、順番の違う文字列は、長さとして読めない。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("45")]
    [InlineData("0m")]
    [InlineData("1x")]
    [InlineData("30m1h")]
    public void RejectsMalformedDurations(string text)
    {
        Assert.False(TimeTextHelper.TryParseDuration(text, out _));
    }

    /// <summary>現地時刻（"HH:mm"）は、0 から 23 時の形だけを読める。</summary>
    [Theory]
    [InlineData("06:30", true, 6, 30)]
    [InlineData("23:59", true, 23, 59)]
    [InlineData("24:00", false, 0, 0)]
    [InlineData("6:30", false, 0, 0)]
    public void ParsesTimeOfDay(string text, bool ok, int hour, int minute)
    {
        var parsed = TimeTextHelper.TryParseTimeOfDay(text, out var time);

        Assert.Equal(ok, parsed);
        if (ok)
        {
            Assert.Equal(new TimeOnly(hour, minute), time);
        }
    }

    /// <summary>月日（"MM-DD"）は存在する日だけを読める。うるう日も読める。</summary>
    [Fact]
    public void ParsesMonthDayAndRejectsNonexistentDays()
    {
        Assert.True(TimeTextHelper.TryParseMonthDay("02-29", out var leapDay));
        Assert.Equal(new MonthDay(2, 29), leapDay);
        Assert.False(TimeTextHelper.TryParseMonthDay("02-30", out _));
        Assert.False(TimeTextHelper.TryParseMonthDay("13-01", out _));
    }

    /// <summary>時差（"+09:00"）は、符号つきの形だけを読める。</summary>
    [Fact]
    public void ParsesUtcOffset()
    {
        Assert.True(TimeTextHelper.TryParseOffset("+09:00", out var tokyo));
        Assert.True(TimeTextHelper.TryParseOffset("-03:30", out var newfoundland));
        Assert.False(TimeTextHelper.TryParseOffset("09:00", out _));

        Assert.Equal(TimeSpan.FromHours(9), tokyo);
        Assert.Equal(TimeSpan.FromHours(-3.5), newfoundland);
    }

    /// <summary>UTC の時刻は、現地の日付と時刻になり、戻すと同じ時刻になる。日付をまたぐ場合も含む。</summary>
    [Fact]
    public void ConvertsBetweenUtcAndLocalTime()
    {
        var utc = new DateTimeOffset(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);
        var offset = TimeSpan.FromHours(9);

        var (date, time) = LocalTimeHelper.ToLocal(utc, offset);

        Assert.Equal(new DateOnly(2026, 11, 2), date);
        Assert.Equal(new TimeOnly(5, 0), time);
        Assert.Equal(utc, LocalTimeHelper.ToUtc(date, time, offset));
    }

    /// <summary>早送りの時計は、待たずに時刻を進め、戻ることはない。</summary>
    [Fact]
    public async Task VirtualClockAdvancesWithoutWaitingAndNeverGoesBack()
    {
        var start = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var clock = new VirtualClock(start);

        await clock.WaitUntilAsync(start.AddHours(3), CancellationToken.None);
        Assert.Equal(start.AddHours(3), clock.UtcNow);

        await clock.WaitUntilAsync(start.AddHours(1), CancellationToken.None);
        Assert.Equal(start.AddHours(3), clock.UtcNow);
    }

    /// <summary>キャンセルされていれば、早送りの時計も待ちを始めない。</summary>
    [Fact]
    public async Task VirtualClockHonoursCancellation()
    {
        var start = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var clock = new VirtualClock(start);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => clock.WaitUntilAsync(start.AddHours(1), cancellation.Token).AsTask());
        Assert.Equal(start, clock.UtcNow);
    }

    /// <summary>壊れた入力（全角の数字、桁あふれ、範囲外、空）は、例外を出さずに false を返す。</summary>
    [Theory]
    [InlineData("99999999999999m")]
    [InlineData("99999999d")]
    [InlineData("４５m")]
    [InlineData("9999999999999999999999999999999999999999d")]
    [InlineData("1h\n")]
    public void MalformedDurationsReturnFalseWithoutThrowing(string text)
    {
        Assert.False(TimeTextHelper.TryParseDuration(text, out _));
    }

    /// <summary>全角の数字や範囲外の時差、時刻、月日は、例外を出さずに読めない。</summary>
    [Theory]
    [InlineData("＋０９:00")]
    [InlineData("+09:60")]
    [InlineData("+15:00")]
    public void MalformedOffsetsAreRejected(string text)
    {
        Assert.False(TimeTextHelper.TryParseOffset(text, out _));
    }

    /// <summary>全角の数字、範囲外の月日と時刻は読めない。例外は出ない。</summary>
    [Fact]
    public void MalformedTimeOfDayAndMonthDayAreRejected()
    {
        Assert.False(TimeTextHelper.TryParseTimeOfDay("１３:00", out _));
        Assert.False(TimeTextHelper.TryParseTimeOfDay("13:60", out _));
        Assert.False(TimeTextHelper.TryParseMonthDay("０１-01", out _));
        Assert.False(TimeTextHelper.TryParseMonthDay("00-00", out _));
        Assert.False(TimeTextHelper.TryParseMonthDay(null, out _));
    }
}
