using Vivace.Cli;

namespace Vivace.Tests.Cli;

/// <summary>--start の読み方。オフセットが必須の ISO 8601 だけを受ける。</summary>
public class StartTimeParserTests
{
    /// <summary>+09:00 の形（分まで、秒まで）と Z を受理し、同じ瞬間を指す。</summary>
    [Theory]
    [InlineData("2026-11-01T23:00+09:00", "2026-11-01T14:00:00Z")]
    [InlineData("2026-11-01T23:00:30+09:00", "2026-11-01T14:00:30Z")]
    [InlineData("2026-11-01T14:00Z", "2026-11-01T14:00:00Z")]
    public void AcceptsOffsetAndZ(string text, string expectedUtc)
    {
        var ok = StartTimeParser.TryParse(text, out var time);

        Assert.True(ok);
        Assert.Equal(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), time);
    }

    /// <summary>オフセットのない時刻、日付だけ、ISO 8601 でない文字列、空は拒否する。</summary>
    [Theory]
    [InlineData("2026-11-01T23:00")]
    [InlineData("2026-11-01T23:00:00")]
    [InlineData("2026-11-01")]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsWithoutOffsetOrNotIso(string? text)
    {
        var ok = StartTimeParser.TryParse(text, out _);

        Assert.False(ok);
    }

    /// <summary>既定の開始は、オフセット付きの ISO 8601 の文字列になり、読み直すと同じ時刻になる。</summary>
    [Fact]
    public void FormatRoundTripsAndIsIso()
    {
        var time = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.FromHours(9));

        var text = StartTimeParser.Format(time);

        Assert.Equal("2026-11-01T23:00+09:00", text);
        Assert.True(StartTimeParser.TryParse(text, out var back));
        Assert.Equal(time, back);
    }
}
