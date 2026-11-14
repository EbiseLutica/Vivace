using System.Globalization;
using System.Text.RegularExpressions;

namespace Vivace.Cli;

/// <summary>開始の時刻の文字列を読む。オフセットが必須の ISO 8601 だけを受け、Z も受ける。</summary>
public static partial class StartTimeParser
{
    /// <summary>受け付ける形。日付、時刻（分まで、または秒まで）、必ずオフセット（+09:00）か Z で終わる。</summary>
    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2})?(Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    /// <summary>形を確かめたあとに読む書式。K は "+09:00" と "Z" を読む。</summary>
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd'T'HH:mm:ssK",
    ];

    /// <summary>開始の時刻を読む。オフセットのない文字列、日付だけの文字列、ISO 8601 でないものは拒否する。</summary>
    /// <param name="text">開始の時刻の文字列。</param>
    /// <param name="time">読めたときの時刻。</param>
    public static bool TryParse(string? text, out DateTimeOffset time)
    {
        time = default;
        return text is not null
            && Shape().IsMatch(text)
            && DateTimeOffset.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>時刻を、オフセット付きの ISO 8601 の文字列にする（カルチャに依らない）。</summary>
    /// <param name="time">時刻。</param>
    public static string Format(DateTimeOffset time) =>
        time.ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
}
