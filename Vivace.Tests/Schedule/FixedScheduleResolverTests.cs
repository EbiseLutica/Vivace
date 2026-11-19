using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Schedule;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Tests.Schedule;

/// <summary>固定予定の解決（期間、例外日、祝日、入れ子、時間帯の並べ書き）を確かめる。</summary>
public class FixedScheduleResolverTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Thursday = new(2026, 12, 24);
    private static readonly DateOnly SummerMonday = new(2026, 7, 27);
    private static readonly DateOnly WinterFriday = new(2026, 12, 25);

    private static readonly DefinitionPack Defs = TestWorld.LoadOrThrow();

    /// <summary>ペルソナの定義を、ID で引く。</summary>
    private static PersonaDef Taro() => Defs.Personas[new PersonaDefId("taro")];

    /// <summary>テスト用の祝日のカレンダー。指定した日だけが祝日。</summary>
    private sealed class FixedHolidays(params DateOnly[] holidays) : IHolidayCalendar
    {
        public bool IsHoliday(DateOnly date) => holidays.Contains(date);
    }

    /// <summary>期間の外（既定の期間）の平日には、授業と入れ子の休み時間が展開される。</summary>
    [Fact]
    public void WeekdayInDefaultPeriodExpandsNestedBlocksInOrder()
    {
        var day = FixedScheduleResolver.Resolve(Defs, Taro(), Monday, NoHolidays.Instance);

        Assert.Empty(day.Issues);
        var expected = new (string Id, string DefId, int Start, int End)[]
        {
            ("school@2026-11-02", "school", 510, 620),
            ("short_break@2026-11-02", "short_break", 620, 630),
            ("school.2@2026-11-02", "school", 630, 740),
            ("lunch_break@2026-11-02", "lunch_break", 740, 780),
            ("school.3@2026-11-02", "school", 780, 870),
            ("short_break.2@2026-11-02", "short_break", 870, 880),
            ("school.4@2026-11-02", "school", 880, 940),
        };
        var school = day.Blocks.Where(block => block.DefId is "school" or "short_break" or "lunch_break").ToList();
        Assert.Equal(expected.Length, school.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Id, school[i].Id);
            Assert.Equal(expected[i].DefId, school[i].DefId);
            Assert.Equal(expected[i].Start, school[i].StartMinute);
            Assert.Equal(expected[i].End, school[i].EndMinute);
        }
    }

    /// <summary>親から引き継いだ行動と場所とスマホの指定が、分割したブロックにも付く。</summary>
    [Fact]
    public void NestedBlocksInheritActionPlaceAndOwnPhone()
    {
        var day = FixedScheduleResolver.Resolve(Defs, Taro(), Monday, NoHolidays.Instance);

        var shortBreak = day.Blocks.Single(block => block.Id == "short_break.2@2026-11-02");
        Assert.Equal("attend_class", shortBreak.Action);
        Assert.Equal("school_2a", shortBreak.Place);
        Assert.Equal(PhonePolicy.Open, shortBreak.Phone);

        var school = day.Blocks.Single(block => block.Id == "school.3@2026-11-02");
        Assert.Equal(PhonePolicy.Closed, school.Phone);

        var lunch = day.Blocks.Single(block => block.DefId == "lunch_break");
        Assert.Equal("eat_lunch", lunch.Action);
        Assert.Equal(["hanako"], lunch.With);
    }

    /// <summary>夏の期間には授業がない。睡眠は、日をまたいで開始日のブロックになる。</summary>
    [Fact]
    public void PeriodDecidesWhichBlocksApply()
    {
        var day = FixedScheduleResolver.Resolve(Defs, Taro(), SummerMonday, NoHolidays.Instance);

        Assert.DoesNotContain(day.Blocks, block => block.DefId == "school");
        var sleep = day.Blocks.Single(block => block.DefId == "sleep_weekday");
        Assert.Equal("sleep_weekday@2026-07-27", sleep.Id);
        Assert.Equal(23 * 60 + 45, sleep.StartMinute);
        Assert.Equal(24 * 60 + 6 * 60 + 30, sleep.EndMinute);
    }

    /// <summary>冬の期間（年をまたぐ）も、期間の中の日に当たる。</summary>
    [Fact]
    public void YearSpanningPeriodMatchesItsDays()
    {
        var periods = Taro().Schedule.Periods;

        Assert.Equal("winter", FixedScheduleResolver.PeriodOf(periods, WinterFriday));
        Assert.Equal("winter", FixedScheduleResolver.PeriodOf(periods, new DateOnly(2027, 1, 3)));
        Assert.Equal("default", FixedScheduleResolver.PeriodOf(periods, new DateOnly(2027, 1, 8)));
    }

    /// <summary>複数の期間に当たるなら、いちばん短いものを使う。</summary>
    [Fact]
    public void ShortestMatchingPeriodWins()
    {
        var wide = new PeriodDef("wide", new MonthDay(1, 1), new MonthDay(12, 31), Source());
        var narrow = new PeriodDef("narrow", new MonthDay(7, 21), new MonthDay(8, 31), Source());

        Assert.Equal("narrow", FixedScheduleResolver.PeriodOf([wide, narrow], new DateOnly(2026, 8, 1)));
        Assert.Equal("wide", FixedScheduleResolver.PeriodOf([wide, narrow], new DateOnly(2026, 2, 1)));
    }

    /// <summary>例外日は、外す指定のブロックを外し、足すブロックを加える。</summary>
    [Fact]
    public void ExceptionDaySkipsAndAddsBlocks()
    {
        var day = FixedScheduleResolver.Resolve(Defs, Taro(), Thursday, NoHolidays.Instance);

        Assert.DoesNotContain(day.Blocks, block => block.DefId == "school");
        Assert.DoesNotContain(day.Blocks, block => block.DefId is "short_break" or "lunch_break");
        var party = day.Blocks.Single(block => block.Id == "xmas_party@2026-12-24");
        Assert.Equal(18 * 60, party.StartMinute);
        Assert.Equal(21 * 60, party.EndMinute);
        Assert.Equal("watch_tv", party.Action);
        Assert.Equal("home_living", party.Place);
    }

    /// <summary>祝日に休むブロックは、祝日には出ない。入れ子の休み時間も、親と一緒に休む。</summary>
    [Fact]
    public void HolidaySkipsBlockAndItsNestedBreaks()
    {
        var holidays = new FixedHolidays(Monday);

        var day = FixedScheduleResolver.Resolve(Defs, Taro(), Monday, holidays);

        Assert.DoesNotContain(day.Blocks, block => block.DefId is "school" or "short_break" or "lunch_break");
        Assert.Contains(day.Blocks, block => block.DefId == "sleep_weekday");
    }

    /// <summary>入れ子は、親の時間の中に収まっている必要がある。外れた子は問題として出る。</summary>
    [Fact]
    public void NestedBlockOutsideParentIsReported()
    {
        var taro = Taro();
        var schedule = taro.Schedule with
        {
            Blocks = taro.Schedule.Blocks.Select(block => block.Id == "short_break"
                ? block with { Ranges = [new TimeRange(new TimeOnly(7, 0), new TimeOnly(7, 10))] }
                : block).ToImmutableArray(),
        };
        var persona = taro with { Schedule = schedule };

        var day = FixedScheduleResolver.Resolve(Defs, persona, Monday, NoHolidays.Instance);

        Assert.Contains(day.Issues, issue => issue.Block.Id == "short_break" && issue.Message.Contains("はみ出して", StringComparison.Ordinal));
        Assert.DoesNotContain(day.Blocks, block => block.DefId == "short_break");
    }

    /// <summary>テスト用の定義の位置（テストの組み立てに使うだけ）。</summary>
    private static DefinitionSource Source() =>
        new(new Vivace.Core.Definitions.SourceLocation("test", 1, 1), ImmutableDictionary<string, Vivace.Core.Definitions.SourceLocation>.Empty);
}
