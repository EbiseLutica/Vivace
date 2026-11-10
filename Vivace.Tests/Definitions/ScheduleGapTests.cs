using System.Collections.Immutable;
using Vivace.Core.Definitions;
using Vivace.Testing;

namespace Vivace.Tests.Definitions;

/// <summary>
/// 固定予定の隙間が、場所の間の移動時間より短いことの警告。定義の検証で、位置つきで出す。
/// </summary>
public class ScheduleGapTests
{
    /// <summary>隙間の警告だけを取り出す。</summary>
    private static ImmutableArray<DefinitionIssue> GapIssues(List<DefinitionFile> files) =>
        DefinitionLoader.LoadFiles(files).Issues.Where(issue => issue.Code == DefinitionIssueCode.ScheduleGap).ToImmutableArray();

    /// <summary>テスト用の町は、隙間の警告が出ない（隙間は移動時間より長い）。</summary>
    [Fact]
    public void TestWorldHasNoScheduleGapWarnings()
    {
        Assert.Empty(GapIssues(TestWorld.Files().ToList()));
    }

    /// <summary>香織の登校を 6:35 にすると、睡眠（6:30 まで、家）との隙間は 5 分で、学校までは 19 分かかる。警告が出る。</summary>
    [Fact]
    public void ShortGapBetweenPlacesWarns()
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "from = \"08:30\"", "from = \"06:35\"");

        var issue = Assert.Single(GapIssues(files));

        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal("kaori: 固定予定の隙間（sleep_weekday と school の間は 5 分）が、移動時間（19 分）より短い", issue.Message);
    }

    /// <summary>例外日だけに足したブロックの隙間も見る。通常の日は、登校が 8:30 なので警告は出ない。</summary>
    [Fact]
    public void ShortGapOnExceptionDateWarns()
    {
        var files = DefinitionFixtures.Replaced(
            "personas/kaori.toml",
            "[[schedule.exception]]",
            "[[schedule.exception]]\ndate = \"2026-11-02\"\n\n[[schedule.exception.block]]\nid = \"early_school\"\nfrom = \"06:35\"\nto = \"07:00\"\naction = \"attend_class\"\nplace = \"school_2a\"\n\n[[schedule.exception]]");

        var issue = Assert.Single(GapIssues(files));

        Assert.Equal("kaori: 固定予定の隙間（sleep_weekday と early_school の間は 5 分）が、移動時間（19 分）より短い", issue.Message);
    }
}
