using System.Globalization;
using System.Text.RegularExpressions;

namespace Vivace.Core.Time;

/// <summary>
/// 時間の文字列の読み書き（D3 3.2）。長さは "45m"、"1h30m"、"3d" のように単位つきで書く。
/// 読みは例外を出さない（Try の名前のものは、どんな入力でも false を返す）。数字は ASCII の 0 から 9 だけを使う。
/// </summary>
public static partial class TimeText
{
    /// <summary>長さの1回の上限（日、時、分の各単位の数字の桁数）。</summary>
    private const int MaxDigits = 9;

    /// <summary>長さの総量の上限（分）。1000 年。</summary>
    private const long MaxTotalMinutes = 1000L * 365 * 24 * 60;

    /// <summary>長さの文字列の形（日、時、分の順に、省略できる単位つき）。</summary>
    [GeneratedRegex(@"^(?:(?<d>[0-9]{1,9})d)?(?:(?<h>[0-9]{1,9})h)?(?:(?<m>[0-9]{1,9})m)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    /// <summary>UTC からの時差の形（"+09:00"）。</summary>
    [GeneratedRegex(@"^(?<sign>[+-])(?<h>[0-9]{2}):(?<m>[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetPattern();

    /// <summary>現地時刻の形（"HH:mm"）。</summary>
    [GeneratedRegex(@"^(?<h>[0-9]{2}):(?<m>[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimeOfDayPattern();

    /// <summary>月日の形（"MM-DD"）。</summary>
    [GeneratedRegex(@"^(?<mo>[0-9]{2})-(?<d>[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex MonthDayPattern();

    /// <summary>長さの文字列を読む。0 や単位のない文字列、上限を超えるものは読めない。</summary>
    /// <param name="text">"45m"、"1h30m"、"3d" など。</param>
    /// <param name="duration">読めたときの長さ。</param>
    public static bool TryParseDuration(string? text, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (text is null)
        {
            return false;
        }

        var match = DurationPattern().Match(text);
        if (!match.Success || !(match.Groups["d"].Success || match.Groups["h"].Success || match.Groups["m"].Success))
        {
            return false;
        }

        var total = ParseGroup(match.Groups["d"]) * 24L * 60
            + ParseGroup(match.Groups["h"]) * 60L
            + ParseGroup(match.Groups["m"]);
        if (total <= 0 || total > MaxTotalMinutes)
        {
            return false;
        }

        duration = TimeSpan.FromMinutes(total);
        return true;
    }

    /// <summary>現地時刻の文字列（"HH:mm"）を読む。</summary>
    /// <param name="text">"06:30" など。</param>
    /// <param name="time">読めたときの時刻。</param>
    public static bool TryParseTimeOfDay(string? text, out TimeOnly time)
    {
        time = default;
        if (text is null)
        {
            return false;
        }

        var match = TimeOfDayPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        if (hour > 23 || minute > 59)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }

    /// <summary>年をまたぐ期間の日付（"MM-DD"）を読む。存在しない月日は読めない。</summary>
    /// <param name="text">"07-21" など。</param>
    /// <param name="monthDay">読めたときの月日。</param>
    public static bool TryParseMonthDay(string? text, out MonthDay monthDay)
    {
        monthDay = default;
        if (text is null)
        {
            return false;
        }

        var match = MonthDayPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var month = int.Parse(match.Groups["mo"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(2000, month))
        {
            return false;
        }

        monthDay = new MonthDay(month, day);
        return true;
    }

    /// <summary>時差の文字列（"+09:00"）を読む。</summary>
    /// <param name="text">時差の文字列。</param>
    /// <param name="offset">読めたときの時差。</param>
    public static bool TryParseOffset(string? text, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (text is null)
        {
            return false;
        }

        var match = OffsetPattern().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        if (hours > 14 || minutes > 59)
        {
            return false;
        }

        offset = new TimeSpan(hours, minutes, 0);
        if (match.Groups["sign"].Value == "-")
        {
            offset = -offset;
        }

        return true;
    }

    private static long ParseGroup(Group group) =>
        group.Success ? long.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture) : 0;
}
