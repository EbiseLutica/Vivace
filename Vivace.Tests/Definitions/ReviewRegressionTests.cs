using System.Collections.Immutable;
using System.Globalization;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Schedule;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Tests.Definitions;

/// <summary>壊した定義で、読み込みが位置つきの問題を返すことを確かめる。</summary>
public class ReviewRegressionTests
{
    private const string Kaori = "personas/kaori.toml";
    private const string Actions = "actions/actions.toml";
    private const string Incidents = "incidents/incidents.toml";
    private const string World = "worlds/testville.toml";
    private const string Shiori = "personas/shiori.toml";

    /// <summary>周期：期間の日付が不正でも例外は出ず、位置つきのエラーになる。</summary>
    [Fact]
    public void ImpossiblePeriodDateIsReportedWithoutThrowing()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "from = \"07-21\"", "from = \"13-40\"");
        var location = DefinitionFixtures.At(files, Kaori, "from = \"13-40\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
        Assert.Null(result.Definitions);
    }

    /// <summary>期間の終わりがなければ、必須のエラーが期間の見出しに出る。例外は出ない。</summary>
    [Fact]
    public void PeriodMissingEndIsReportedAtHeader()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "to = \"08-31\"\n", string.Empty);
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.period]]\nid = \"summer\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.MissingRequired, header);
    }

    /// <summary>長さの文字列が桁あふれや全角の数字でも、例外は出ず、位置つきの値のエラーになる。</summary>
    [Theory]
    [InlineData("99999999999999m")]
    [InlineData("99999999d")]
    [InlineData("４５m")]
    public void OverflowingOrFullWidthDurationIsReported(string text)
    {
        var files = DefinitionFixtures.Replaced(Incidents, "cooldown = \"7d\"", $"cooldown = \"{text}\"");
        var location = DefinitionFixtures.At(files, Incidents, $"cooldown = \"{text}\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>単位が d、h、m 以外（"7w"）は読めず、値のエラーになる。</summary>
    [Fact]
    public void DurationWithUnknownUnitIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "cooldown = \"7d\"", "cooldown = \"7w\"");
        var location = DefinitionFixtures.At(files, Incidents, "cooldown = \"7w\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>移動時間は 1 以上の整数。0、負の値、int に入らない値は、値のエラーになる。</summary>
    [Theory]
    [InlineData("park = 0")]
    [InlineData("park = -3")]
    [InlineData("park = 99999999999")]
    public void LinkMinutesMustBePositiveInt(string link)
    {
        var files = DefinitionFixtures.Replaced(World, "park = 10", link);
        var location = DefinitionFixtures.At(files, World, link);

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>schedule_only でない行動に所要時間がなければ、行動の見出しに必須のエラーが出る。</summary>
    [Fact]
    public void NonScheduleActionWithoutDurationIsReported()
    {
        var files = DefinitionFixtures.Replaced(Actions, "duration = [\"30m\", \"90m\"]\n", string.Empty);
        var header = DefinitionFixtures.At(files, Actions, "[[action]]\nid = \"read_book\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.MissingRequired, header);
    }

    /// <summary>core.go_to_next は所要時間を持たなくてよい（次の予定で決まる）。組み込みは読める。</summary>
    [Fact]
    public void GoToNextMayHaveNoDuration()
    {
        var result = TestWorld.Load();

        Assert.Empty(result.Issues);
        Assert.Null(result.Definitions!.Actions["core.go_to_next"].Duration);
    }

    /// <summary>自分自身を親にした within は、循環としてエラーになる。</summary>
    [Fact]
    public void WithinSelfReferenceIsReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "id = \"sleep_weekday\"", "id = \"sleep_weekday\"\nwithin = \"sleep_weekday\"");
        var location = DefinitionFixtures.At(files, Kaori, "within = \"sleep_weekday\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>2 つのブロックが互いを親にすれば、循環としてエラーになる。解決は止まらない。</summary>
    [Fact]
    public void WithinCycleIsReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "id = \"school\"", "id = \"school\"\nwithin = \"short_break\"");
        var location = DefinitionFixtures.At(files, Kaori, "within = \"short_break\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>同じブロックの times どうしが重なれば、ブロックの見出しに重なりのエラーが出る。</summary>
    [Fact]
    public void OverlappingTimesOfOneBlockAreReported()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "times = [[\"10:20\", \"10:30\"], [\"14:30\", \"14:40\"]]",
            "times = [[\"10:20\", \"10:30\"], [\"10:25\", \"10:40\"], [\"14:30\", \"14:40\"]]");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.block]]\nid = \"short_break\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("同じブロックの時間帯が重なって", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>同じ親の中で、子どうしが重なれば、後の子の見出しにエラーが出る。</summary>
    [Fact]
    public void OverlappingChildrenOfSameParentAreReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "from = \"12:20\"\nto = \"13:00\"", "from = \"10:25\"\nto = \"10:35\"");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.block]]\nid = \"lunch_break\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("同じ親の中で子のブロックが重なって", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>親の時間からはみ出した子は、子の見出しに、はみ出しのエラーが出る。</summary>
    [Fact]
    public void ChildOutsideParentIsReported()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "times = [[\"10:20\", \"10:30\"], [\"14:30\", \"14:40\"]]",
            "times = [[\"07:00\", \"07:10\"]]");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.block]]\nid = \"short_break\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("はみ出して", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 日またぎの重なりは、前日からの睡眠との重なりを、定義ごとに1件だけ出す（同じ内容を何百件も出さない）。
    /// </summary>
    [Fact]
    public void CrossMidnightOverlapIsReportedOncePerDefinition()
    {
        var dawn = "[[schedule.block]]\nid = \"dawn\"\nfrom = \"00:30\"\nto = \"01:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"\n\n[[schedule.exception]]\n";
        var files = DefinitionFixtures.Replaced(Kaori, "[[schedule.exception]]\n", dawn);
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.block]]\nid = \"dawn\"");

        var result = DefinitionLoader.LoadFiles(files);

        var dawnIssues = result.Issues.Where(issue => issue.Code == DefinitionIssueCode.ScheduleOverlap && issue.Message.Contains("dawn", StringComparison.Ordinal)).ToList();
        var issue = Assert.Single(dawnIssues);
        Assert.Equal(header, issue.Location);
    }

    /// <summary>例外日は前日の睡眠との重なりも見る（12-24 の 05:00〜07:00 と、前夜の睡眠〜06:30）。</summary>
    [Fact]
    public void ExceptionDayIsCheckedAgainstPreviousNight()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "place = \"home_living\"",
            "place = \"home_living\"\n\n[[schedule.exception.block]]\nid = \"early\"\nfrom = \"05:00\"\nto = \"07:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.exception.block]]\nid = \"early\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("前日から続くブロック", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>例外日が 2028〜2055 の外でも、その前後の日を見る。</summary>
    [Fact]
    public void ExceptionOutsideTheScanYearsIsStillChecked()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "place = \"home_living\"",
            "place = \"home_living\"\n\n[[schedule.exception]]\ndate = \"2060-01-01\"\n\n[[schedule.exception.block]]\nid = \"far\"\nfrom = \"05:00\"\nto = \"07:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.exception.block]]\nid = \"far\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
    }

    /// <summary>例外日のブロックに in を書けば、エラーになる。</summary>
    [Fact]
    public void ExceptionBlockWithInIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "id = \"xmas_party\"", "id = \"xmas_party\"\nin = [\"default\"]");
        var location = DefinitionFixtures.At(files, Kaori, "id = \"xmas_party\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>例外日の skip は基本のブロックだけに当たる。同じ ID で足したブロックは消えない。</summary>
    [Fact]
    public void ExceptionSkipDoesNotRemoveAddedBlockWithSameId()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "place = \"home_living\"",
            "place = \"home_living\"\n\n[[schedule.exception.block]]\nid = \"school\"\nfrom = \"09:00\"\nto = \"10:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"");
        var result = DefinitionLoader.LoadFiles(files);
        Assert.True(result.Definitions is not null, string.Join(" | ", result.Issues.Select(issue => issue.ToString())));
        var definitions = result.Definitions!;

        var day = FixedScheduleResolver.Resolve(definitions, definitions.Personas[new PersonaDefId("kaori")], new DateOnly(2026, 12, 24), NoHolidays.Instance);

        Assert.Contains(day.Blocks, block => block.Id == "school@2026-12-24" && block.StartMinute == 540);
    }

    /// <summary>場所、ブロックの ID に名前空間の "." や "@" は使えない。</summary>
    [Theory]
    [InlineData(World, "id = \"street\"", "id = \"st.reet\"")]
    [InlineData(Kaori, "id = \"school\"", "id = \"lunch.2@x\"")]
    public void PlainIdsRejectDotsAndAt(string path, string from, string to)
    {
        var files = DefinitionFixtures.Replaced(path, from, to);
        var location = DefinitionFixtures.At(files, path, to);

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>行動の ID が空なら、値のエラーになる（黙って捨てない）。</summary>
    [Fact]
    public void EmptyActionIdIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Actions, "id = \"walk_park\"", "id = \"\"");
        var location = DefinitionFixtures.At(files, Actions, "id = \"\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>使う行動のワイルドカードに当たる行動がなくても、読める。警告にする。</summary>
    [Fact]
    public void UnmatchedWildcardIsWarnedNotFailed()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "use = [\"core.*\", \"walk_park\"", "use = [\"core.*\", \"misskey.*\", \"walk_park\"");
        var location = DefinitionFixtures.At(files, Kaori, "use = [\"core.*\", \"misskey.*\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.UnmatchedWildcard, location);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.NotNull(result.Definitions);
    }

    /// <summary>確率に nan は使えない。値のエラーになる。</summary>
    [Fact]
    public void NanChanceIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "chance = 0.03", "chance = nan");
        var location = DefinitionFixtures.At(files, Incidents, "chance = nan");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>好みの数は 0 以上。負の倍率は値のエラーになる。</summary>
    [Fact]
    public void NegativePreferenceFactorIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "cafe = 1.5", "cafe = -1.5");
        var location = DefinitionFixtures.At(files, Kaori, "cafe = -1.5");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>負の乱れの重みは値のエラーになる。</summary>
    [Fact]
    public void NegativeDisruptionWeightIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "late = 1.0", "late = -1.0");
        var location = DefinitionFixtures.At(files, Kaori, "late = -1.0");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>repeat_decay は 0 より大きく 1 以下。5.0 は値のエラーになる。</summary>
    [Fact]
    public void RepeatDecayOutOfRangeIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Actions, "effects.repeat_decay = 0.6", "effects.repeat_decay = 5.0");
        var location = DefinitionFixtures.At(files, Actions, "effects.repeat_decay = 5.0");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>slow_place は場所（place か place_tag）のどちらか一方が必要。</summary>
    [Theory]
    [InlineData("effects.slow_place = { add = \"15m\" }")]
    [InlineData("effects.slow_place = { place = \"street\", place_tag = \"road\", add = \"15m\" }")]
    public void SlowPlaceNeedsExactlyOnePlaceChoice(string replacement)
    {
        var files = DefinitionFixtures.Replaced(Incidents, "effects.slow_place = { place_tag = \"road\", add = \"15m\" }", replacement);

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == Incidents);
    }

    /// <summary>slow_place は add か factor の少なくとも一方が必要。</summary>
    [Fact]
    public void SlowPlaceNeedsAddOrFactor()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "effects.slow_place = { place_tag = \"road\", add = \"15m\" }", "effects.slow_place = { place_tag = \"road\" }");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == Incidents);
    }

    /// <summary>ワールド向けの出来事に状態（condition）を付けると、値のエラーになる。</summary>
    [Fact]
    public void WorldTargetWithConditionIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "target = \"persona\"", "target = \"world\"");
        var location = DefinitionFixtures.At(files, Incidents, "effects.condition = \"cold\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>同じ表の見出しを2回書けば、重複のエラーになる。</summary>
    [Fact]
    public void DuplicateTableHeaderIsReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "[memory]\n", "[memory]\nhalf_life = 1.2\n[memory]\n");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Severity == IssueSeverity.Error
            && (issue.Code == DefinitionIssueCode.DuplicateKey || issue.Code == DefinitionIssueCode.SyntaxError)
            && issue.Location.Path == Kaori);
        Assert.Null(result.Definitions);
    }

    /// <summary>チャンネルの設定は、不変の JSON の要素として読める（プラグインが読む）。</summary>
    [Fact]
    public void ChannelSettingsAreKeptAsJsonElement()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "follow_back = \"always\"", "follow_back = \"always\"\n\n[channel.misskey]\nreversi = true");

        var definitions = Assert.IsType<DefinitionPack>(DefinitionLoader.LoadFiles(files).Definitions);

        Assert.True(definitions.Personas[new PersonaDefId("kaori")].ChannelSettings["misskey"].GetProperty("reversi").GetBoolean());
    }

    /// <summary>ファイルの渡す順を変えても、問題の一覧とハッシュは変わらない。</summary>
    [Fact]
    public void FileOrderDoesNotChangeIssuesOrHash()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "home = \"kaori_room\"", "home = \"nowhere\"");
        var reversed = files.AsEnumerable().Reverse().ToList();

        var forward = DefinitionLoader.LoadFiles(files);
        var backward = DefinitionLoader.LoadFiles(reversed);

        Assert.Equal(forward.Issues, backward.Issues);
        Assert.Equal(forward.Definitions is null, backward.Definitions is null);
    }

    /// <summary>場所のつながりは ID の順に並ぶ。</summary>
    [Fact]
    public void PlaceLinksAreSortedById()
    {
        var street = TestWorld.LoadOrThrow().Worlds[TestWorld.DefId].Places.Single(place => place.Id == "street");

        Assert.Equal(["cafe", "park", "school_2a"], street.Links.Select(link => link.To).ToArray());
    }

    /// <summary>要素の型が違えば、位置つきの値のエラーになる。</summary>
    [Fact]
    public void WrongValueTypeIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Actions, "weight = 1.0", "weight = \"heavy\"");
        var location = DefinitionFixtures.At(files, Actions, "weight = \"heavy\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>効果の語彙にないキーは、未知のキーの警告になる。定義は読める。</summary>
    [Fact]
    public void UnknownEffectKeyIsWarned()
    {
        var files = DefinitionFixtures.Replaced(Actions, "effects.repeat_decay = 0.6", "effects.repeat_decay = 0.6\neffects.unknown_effect = 1");
        var location = DefinitionFixtures.At(files, Actions, "effects.unknown_effect = 1");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.UnknownKey, location);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.NotNull(result.Definitions);
    }

    /// <summary>好みのキーにない行動があれば、参照のエラーになる。</summary>
    [Fact]
    public void PreferenceForUnknownActionIsReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "prefs = { read_book = \"like\"", "prefs = { nonexistent = \"like\", read_book = \"like\"");
        var location = DefinitionFixtures.At(files, Kaori, "nonexistent = \"like\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.MissingReference, location);
    }

    /// <summary>
    /// 親の子ではない場所の一覧（同行のブロックのずれ）。同行の相手と時刻、場所、行動がそろわなければ、警告になる。
    /// </summary>
    [Fact]
    public void PartnerWithDifferentPlaceIsWarned()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "place = \"school_2a\"\nphone = \"open\"", "place = \"street\"\nphone = \"open\"");
        var location = DefinitionFixtures.At(files, Kaori, "with = [\"shiori\"]");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.PartnerMismatch, location);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    /// <summary>
    /// 警告：requires.items を持たない行動は、一度も実行できないので警告になる（データはそろっている）。
    /// </summary>
    [Fact]
    public void ItemRequirementNeverSatisfiedIsWarned()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "items = [\"smartphone\", \"book\"]", "items = [\"smartphone\"]");
        var location = DefinitionFixtures.At(files, Kaori, "use = [\"core.*\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.UnsatisfiableRequirement, location);
        Assert.Contains("携行品", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// カルチャが変わっても、ブロックの ID の日付は西暦の "yyyy-MM-dd" のまま。
    /// </summary>
    [Fact]
    public void BlockIdsDoNotDependOnCulture()
    {
        var definitions = TestWorld.LoadOrThrow();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var day = FixedScheduleResolver.Resolve(definitions, definitions.Personas[new PersonaDefId("kaori")], new DateOnly(2026, 11, 2), NoHolidays.Instance);

            Assert.Contains(day.Blocks, block => block.Id == "school@2026-11-02");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// 2028 年には重ならず、28 年周期の別の年（2038 年は 1 月 1 日が金曜）にだけ重なるブロックは、解決で見つかる。
    /// </summary>
    [Fact]
    public void OverlapInOnlyALaterYearOfTheCycleIsFound()
    {
        var source = new DefinitionSource(new SourceLocation(Kaori, 1, 1), ImmutableDictionary<string, SourceLocation>.Empty);
        var definitions = TestWorld.LoadOrThrow();
        var kaori = definitions.Personas[new PersonaDefId("kaori")];
        var period = new PeriodDef("ny", new MonthDay(1, 1), new MonthDay(1, 1), source);
        var fridayOnly = new BlockDef("ny_a", null, ["ny"], [DayOfWeek.Friday], [new TimeRange(new TimeOnly(12, 0), new TimeOnly(13, 0))], "core.idle", "kaori_room", null, null, [], null, null, source);
        var everyYear = new BlockDef("ny_b", null, ["ny"], [], [new TimeRange(new TimeOnly(12, 0), new TimeOnly(13, 0))], "core.idle", "kaori_room", null, null, [], null, null, source);
        var persona = kaori with
        {
            Schedule = kaori.Schedule with
            {
                Periods = kaori.Schedule.Periods.Add(period),
                Blocks = kaori.Schedule.Blocks.Add(fridayOnly).Add(everyYear),
            },
        };

        var in2028 = FixedScheduleResolver.Resolve(definitions, persona, new DateOnly(2028, 1, 1), NoHolidays.Instance);
        var in2038 = FixedScheduleResolver.Resolve(definitions, persona, new DateOnly(2038, 1, 1), NoHolidays.Instance);

        Assert.DoesNotContain(in2028.Issues, issue => issue.Message.Contains("ny_", StringComparison.Ordinal));
        Assert.Contains(in2038.Issues, issue => issue.Message.Contains("ny_a", StringComparison.Ordinal));
    }

    /// <summary>同じ重なりを定義ファイルに書けば、読み込みのエラーとして、後のブロックの見出しに出る。</summary>
    [Fact]
    public void OverlapInLaterCycleYearIsReportedByLoader()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "[[schedule.exception]]\n",
            "[[schedule.period]]\nid = \"ny\"\nfrom = \"01-01\"\nto = \"01-01\"\n\n[[schedule.block]]\nid = \"ny_a\"\nin = [\"ny\"]\ndays = [\"fri\"]\nfrom = \"12:00\"\nto = \"13:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"\n\n[[schedule.block]]\nid = \"ny_b\"\nin = [\"ny\"]\nfrom = \"12:00\"\nto = \"13:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"\n\n[[schedule.exception]]\n");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.block]]\nid = \"ny_b\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("ny_a", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 数は有限で範囲内。nan、inf、範囲外、int に入らない値、target と効果の組み合わせの違いは、位置つきの値のエラーになる。
    /// </summary>
    [Theory]
    [InlineData("actions/actions.toml", "weight = 1.0", "weight = nan")]
    [InlineData("actions/actions.toml", "weight = 1.0", "weight = inf")]
    [InlineData("personas/kaori.toml", "half_life = 1.2", "half_life = nan")]
    [InlineData("personas/kaori.toml", "energy = { initial = 80 }", "energy = { initial = nan }")]
    [InlineData("personas/kaori.toml", "energy = { initial = 80 }", "energy = { initial = 500 }")]
    [InlineData("personas/kaori.toml", "floor = -40", "floor = nan")]
    [InlineData("personas/kaori.toml", "rate_alone = 2.0", "rate_alone = nan")]
    [InlineData("personas/kaori.toml", "threshold = 15,", "threshold = nan,")]
    [InlineData("personas/kaori.toml", "threshold = 15,", "threshold = 500,")]
    [InlineData("actions/actions.toml", "limits.max_per_day = 2", "limits.max_per_day = 5000000000")]
    [InlineData("incidents/incidents.toml", "weight.\"tag:outdoor\" = 0.3", "weight.\"tag:outdoor\" = -0.3")]
    [InlineData("incidents/incidents.toml", "weight.\"tag:outdoor\" = 0.3", "weight.\"tag:outdoor\" = nan")]
    [InlineData("incidents/incidents.toml", "effects.slow_place = { place_tag = \"road\", add = \"15m\" }", "effects.slow_place = { place_tag = \"road\", add = \"15m\" }\neffects.vital = { energy = -20 }")]
    [InlineData("incidents/incidents.toml", "effects.condition = \"cold\"", "effects.condition = \"cold\"\neffects.slow_place = { place_tag = \"road\", add = \"15m\" }")]
    [InlineData("incidents/incidents.toml", "effects.condition = \"cold\"", "effects.condition = \"cold\"\neffects.close_place = { place_tag = \"road\" }")]
    public void OutOfRangeOrNonFiniteValuesAreRejected(string path, string needle, string replacement)
    {
        var files = DefinitionFixtures.Replaced(path, needle, replacement);

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Severity == IssueSeverity.Error && issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == path);
        Assert.Null(result.Definitions);
    }

    /// <summary>所要時間が読めなければ、値のエラーだけを出し、欠落のエラーを重ねない。</summary>
    [Fact]
    public void UnreadableDurationIsNotAlsoReportedAsMissing()
    {
        var files = DefinitionFixtures.Replaced(Actions, "duration = [\"30m\", \"90m\"]", "duration = \"1w\"");
        var header = DefinitionFixtures.At(files, Actions, "[[action]]\nid = \"read_book\"");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == DefinitionIssueCode.MissingRequired && issue.Location == header);
        Assert.Contains(result.Issues, issue => issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == Actions);
    }

    /// <summary>自由時間の開始は ID を持たない。固定と計画のブロックの開始は ID を持つ。</summary>
    [Fact]
    public void BlockStartedMayHaveNoIdForFreeTime()
    {
        var free = new BlockStartedPayload(null, 1);
        var fixedBlock = new BlockStartedPayload("school@2026-11-02", 1);

        Assert.Null(free.BlockId);
        Assert.Equal("school@2026-11-02", fixedBlock.BlockId);
    }

    /// <summary>率は −1000〜1000、復元は気力などが 0〜100、社交欲求が −100〜100。範囲の外は位置つきの値のエラーになる。</summary>
    [Theory]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.rate.energy = 1e300")]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.rate.energy = 5000")]
    [InlineData("incidents/incidents.toml", "rate.health = -0.5", "rate.health = 5000")]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.restore.energy = 101")]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.restore.energy = -1")]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.restore.social = 101")]
    [InlineData("actions/actions.toml", "effects.on_end.boredom = -15", "effects.on_end.boredom = -15\neffects.restore.social = -101")]
    public void RatesAndRestoresStayInRange(string path, string needle, string replacement)
    {
        var files = DefinitionFixtures.Replaced(path, needle, replacement);

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Severity == IssueSeverity.Error && issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == path);
        Assert.Null(result.Definitions);
    }

    /// <summary>社交欲求の復元は −100〜100 の中なら読める。</summary>
    [Fact]
    public void SocialRestoreInsideRangeIsAccepted()
    {
        var files = DefinitionFixtures.Replaced(Actions, "effects.repeat_decay = 0.6", "effects.repeat_decay = 0.6\neffects.restore.social = -100");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
    }

    /// <summary>社交欲求にはしきい値がない。反射に書けばエラーになる。</summary>
    [Fact]
    public void SocialReflexIsRejected()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "health = { threshold = 15, action = \"core.go_home_rest\" }",
            "health = { threshold = 15, action = \"core.go_home_rest\" }\nsocial = { threshold = 10, action = \"core.idle\" }");
        var location = DefinitionFixtures.At(files, Kaori, "social = { threshold = 10");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
    }

    /// <summary>状態のしきい値にも社交欲求は書けない。</summary>
    [Fact]
    public void SocialConditionThresholdIsRejected()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "threshold.health = 25", "threshold.health = 25\nthreshold.social = 10");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Severity == IssueSeverity.Error && issue.Code == DefinitionIssueCode.InvalidValue && issue.Location.Path == Incidents);
    }

    /// <summary>上限のない重みは、どんな有限の数も読める（範囲の文面を出さない）。</summary>
    [Fact]
    public void UnboundedWeightAcceptsLargeFiniteNumbers()
    {
        var files = DefinitionFixtures.Replaced(Actions, "weight = 1.0", "weight = 1e300");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error && issue.Location.Path == Actions);
    }

    /// <summary>範囲のエラーの文面は、読める形の数（カルチャに依らず、1.79E+308 のような数を出さない）。</summary>
    [Fact]
    public void RangeMessageUsesPlainInvariantBounds()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "rate_alone = 2.0", "rate_alone = 5000");
        var location = DefinitionFixtures.At(files, Kaori, "rate_alone = 5000");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
        Assert.Contains("-1000", issue.Message, StringComparison.Ordinal);
        Assert.Contains("1000", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("E+", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同行の相手の例外日が範囲の外（2027 年）でも、範囲の中（2030 年）でも、香織への警告は同じ（1件、同じ文面）。
    /// </summary>
    [Theory]
    [InlineData("2027-03-15")]
    [InlineData("2030-03-15")]
    public void PartnerExceptionOutsideOrInsideScanGivesSameWarning(string date)
    {
        var files = DefinitionFixtures.Replaced(
            Shiori,
            "with = [\"kaori\"]",
            "with = [\"kaori\"]\n\n[[schedule.exception]]\ndate = \"" + date + "\"\nskip = [\"school\"]");

        var result = DefinitionLoader.LoadFiles(files);

        var kaoriWarnings = result.Issues.Where(issue => issue.Code == DefinitionIssueCode.PartnerMismatch && issue.Location.Path == Kaori).ToList();
        var single = Assert.Single(kaoriWarnings);
        Assert.Contains("lunch_break", single.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(date, single.Message, StringComparison.Ordinal);
    }

    /// <summary>読めない例外日（不正な日付、欠落）は、例外にならず、位置つきのエラーになる。</summary>
    [Theory]
    [InlineData("2026-13-40")]
    [InlineData("not-a-date")]
    public void InvalidExceptionDateIsReportedNotThrown(string date)
    {
        var files = DefinitionFixtures.Replaced(Kaori, "date = \"2026-12-24\"", $"date = \"{date}\"");
        var location = DefinitionFixtures.At(files, Kaori, $"date = \"{date}\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
        Assert.Null(result.Definitions);
    }

    /// <summary>例外日の欠落は、必須のエラーになる。例外にはならない。</summary>
    [Fact]
    public void MissingExceptionDateIsReported()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "date = \"2026-12-24\"\n", string.Empty);
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.exception]]\nskip");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue => issue.Code == DefinitionIssueCode.MissingRequired && issue.Location.Path == Kaori);
        Assert.Null(result.Definitions);
    }

    /// <summary>日付の範囲の両端（0001-01-01、9999-12-31）の例外日は、前後の日を越えて見ず、例外にならない。</summary>
    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("9999-12-31")]
    public void ExceptionAtDateLimitsDoesNotThrow(string date)
    {
        var files = DefinitionFixtures.Replaced(Kaori, "date = \"2026-12-24\"", $"date = \"{date}\"");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error && issue.Code != DefinitionIssueCode.ScheduleOverlap);
    }

    /// <summary>例外日が 2028-01-01（範囲の端）でも、前夜の睡眠との重なりを見る。</summary>
    [Fact]
    public void ExceptionOnFirstDayOfScanIsCheckedAgainstPreviousNight()
    {
        var files = DefinitionFixtures.Replaced(
            Kaori,
            "place = \"home_living\"",
            "place = \"home_living\"\n\n[[schedule.exception]]\ndate = \"2028-01-01\"\n\n[[schedule.exception.block]]\nid = \"ny_early\"\nfrom = \"05:00\"\nto = \"07:00\"\naction = \"core.idle\"\nplace = \"kaori_room\"");
        var header = DefinitionFixtures.At(files, Kaori, "[[schedule.exception.block]]\nid = \"ny_early\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("前日から続くブロック", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>同行の不一致は、ブロックの日ごとではなく定義ごとに1件だけ出る。</summary>
    [Fact]
    public void PartnerMismatchIsReportedOncePerDefinition()
    {
        var files = DefinitionFixtures.Replaced(Kaori, "place = \"school_2a\"\nphone = \"open\"", "place = \"street\"\nphone = \"open\"");

        var result = DefinitionLoader.LoadFiles(files);

        var mismatches = result.Issues.Where(issue => issue.Code == DefinitionIssueCode.PartnerMismatch).ToList();
        Assert.Equal(2, mismatches.Count);
        Assert.All(mismatches.GroupBy(issue => issue.Location.Path), group => Assert.Single(group));
        Assert.All(mismatches, issue =>
        {
            Assert.Contains("lunch_break", issue.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("@", issue.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>出来事の発生は、移動時間と閉鎖の両方の解決済みの効果を残せる。定義がなくてもよい（その場の定義）。</summary>
    [Fact]
    public void IncidentPayloadKeepsBothSlowAndClosedEffects()
    {
        var effects = new ResolvedIncidentEffects(
            new ResolvedSlowPlace(new PlaceId("street"), TimeSpan.FromMinutes(15), null),
            new PlaceId("park"),
            ImmutableDictionary<VitalName, double>.Empty,
            null);

        var payload = new IncidentOccurredPayload(null, null, null, effects);

        Assert.Null(payload.DefId);
        Assert.Equal(new PlaceId("street"), payload.Effects.SlowPlace!.Place);
        Assert.Equal(new PlaceId("park"), payload.Effects.ClosedPlace);
    }

    /// <summary>曜日を省略したブロックは毎日使う。</summary>
    [Fact]
    public void BlockWithoutDaysAppliesEveryDay()
    {
        var definitions = TestWorld.LoadOrThrow();

        var saturday = FixedScheduleResolver.Resolve(definitions, definitions.Personas[new PersonaDefId("kaori")], new DateOnly(2026, 11, 7), NoHolidays.Instance);

        Assert.Contains(saturday.Blocks, block => block.DefId == "sleep_weekday");
        Assert.DoesNotContain(saturday.Blocks, block => block.DefId == "school");
    }

    /// <summary>片側判定：同行の相手の例外日のブロックに同行が書いてあれば、片側とはしない。</summary>
    [Fact]
    public void OneSidedWithAcceptsPartnerExceptionBlock()
    {
        var files = DefinitionFixtures.Replaced(Shiori, "with = [\"kaori\"]", string.Empty);
        files = files.Select(file => file.Path == Shiori
            ? file with { Text = file.Text + "\n[[schedule.exception]]\ndate = \"2026-12-24\"\n\n[[schedule.exception.block]]\nid = \"xmas_lunch\"\nfrom = \"12:20\"\nto = \"13:00\"\naction = \"eat_lunch\"\nplace = \"school_2a\"\nwith = [\"kaori\"]\n" }
            : file).ToList();

        var result = DefinitionLoader.LoadFiles(files);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == DefinitionIssueCode.OneSidedWith);
    }

    /// <summary>出来事の効果が指す場所がどのワールドにもなければ、参照のエラーになる。</summary>
    [Fact]
    public void IncidentPlaceMustExist()
    {
        var files = DefinitionFixtures.Replaced(Incidents, "effects.slow_place = { place_tag = \"road\", add = \"15m\" }", "effects.slow_place = { place = \"nowhere\", add = \"15m\" }");
        var location = DefinitionFixtures.At(files, Incidents, "place = \"nowhere\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.MissingReference, location);
    }
}
