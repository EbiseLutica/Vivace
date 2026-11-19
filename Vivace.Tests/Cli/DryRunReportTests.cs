using Vivace.Cli;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Cli;

/// <summary>行動表の組み立て。手で書いた小さな記録と、テスト用の定義の名前から、行の形、閉じ方、日と人の並び、件数を確かめる。</summary>
public class DryRunReportTests
{
    private static readonly DisplayNames Names = new(TestWorld.LoadOrThrow());
    private static readonly PersonaId Kaori = new("kaori");
    private static readonly PersonaId Shiori = new("shiori");
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");
    private static readonly PlaceId School = new("school_2a");
    private static readonly WorldId Town = new("testville");

    /// <summary>
    /// 時差は UTC+9。UTC の 2026-11-01 22:00 は、現地の 2026-11-02 07:00（月曜）。
    /// 行は、同じ人の終わりの記録で閉じる。捨てた記録と却下の記録は行にせず、件数だけ数える（捨てた 2 件、却下 1 件で入れ替わりを見る）。
    /// 人は ID の序数順（kaori、shiori）、記録の順には依らない。
    /// </summary>
    [Fact]
    public void RendersRowsByDayAndPersonaWithCounts()
    {
        var records = new[]
        {
            Logged(1, At(22, 30), new ActionStartedPayload("read_book", HomeLiving, TimeSpan.FromMinutes(20), ActivityReason.Chosen, 1, null), Shiori),
            Logged(2, At(22, 0), new ActionStartedPayload("eat_breakfast", KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Block, 1, "b@1"), Kaori),
            Logged(3, At(22, 30), new ActionEndedPayload("eat_breakfast", 1), Kaori),
            Logged(4, At(23, 10), new TravelStartedPayload(KaoriRoom, School, [], At(23, 25), 2), Kaori),
            Logged(5, At(23, 12), new ActionEndedPayload("read_book", 1), Kaori, EventStatus.Skipped),
            Logged(6, At(23, 25), new TravelArrivedPayload(School, 2), Kaori),
            Logged(7, At(23, 25), new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(60), ActivityReason.Chosen, 3, null), Kaori),
            Logged(8, At(22, 50), new ActionEndedPayload("read_book", 1), Shiori),
            Logged(9, At(23, 40), new TravelStartedPayload(KaoriRoom, HomeLiving, [], At(23, 55), 4), Kaori, EventStatus.Rejected),
            Logged(10, At(23, 13), new ActionEndedPayload("attend_class", 1), Kaori, EventStatus.Skipped),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-02（月）\n"
            + "香織\n"
            + "  07:00-07:30 eat_breakfast＠香織の部屋（予定）\n"
            + "  08:10-08:25 移動 香織の部屋 → 2年A組の教室\n"
            + "  08:25-未終了 授業を受ける＠2年A組の教室（自由）\n"
            + "詩織\n"
            + "  07:30-07:50 本を読む＠居間（自由）\n"
            + "\n"
            + "適用 7 件、捨てた 2 件、却下 1 件\n",
            text);
    }

    /// <summary>
    /// 終わりの記録がなくても、次の行が始まれば、その開始の時刻で閉じる。反射と遅刻の表記も確かめる。
    /// 記録の終わりまで続いた行だけが「未終了」になる。
    /// </summary>
    [Fact]
    public void ClosesUnfinishedRowAtNextStartAndLabelsReflexAndLate()
    {
        var records = new[]
        {
            Logged(1, At(1, 0), new ActionStartedPayload("read_book", HomeLiving, TimeSpan.FromMinutes(30), ActivityReason.Reflex, 1, null), Kaori),
            Logged(2, At(1, 30), new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(30), ActivityReason.Late, 2, null), Kaori),
            Logged(3, At(2, 0), new TravelStartedPayload(School, KaoriRoom, [], At(2, 20), 3), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-01（日）\n"
            + "香織\n"
            + "  10:00-10:30 本を読む＠居間（反射）\n"
            + "  10:30-11:00 授業を受ける＠2年A組の教室（遅刻）\n"
            + "  11:00-未終了 移動 2年A組の教室 → 香織の部屋\n"
            + "\n"
            + "適用 3 件、捨てた 0 件、却下 0 件\n",
            text);
    }

    /// <summary>行がない記録でも、件数は出る。捨てた 2 件と却下 1 件を、入れ替えずに数える。</summary>
    [Fact]
    public void SkippedAndRejectedAreCountedSeparately()
    {
        var records = new[]
        {
            Logged(1, At(1, 0), new ActionStartedPayload("x", HomeLiving, TimeSpan.FromMinutes(5), ActivityReason.Chosen, 1, null), Kaori, EventStatus.Skipped),
            Logged(2, At(1, 10), new ActionEndedPayload("x", 1), Kaori, EventStatus.Skipped),
            Logged(3, At(1, 20), new TravelStartedPayload(KaoriRoom, School, [], At(1, 30), 1), Kaori, EventStatus.Rejected),
            Logged(4, At(2, 0), new ActionStartedPayload("read_book", HomeLiving, TimeSpan.FromMinutes(20), ActivityReason.Chosen, 2, null), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-01（日）\n"
            + "香織\n"
            + "  11:00-未終了 本を読む＠居間（自由）\n"
            + "\n"
            + "適用 1 件、捨てた 2 件、却下 1 件\n",
            text);
    }

    /// <summary>
    /// 副行動の行は、主の行動の下に1段深く、「（スマホ）」つきで出る。副行動の終わりの記録がないまま主が終われば、主の終わりの時刻で閉じる。
    /// UTC の 22:00 は現地の 07:00（11-02）。
    /// </summary>
    [Fact]
    public void SubActionRowIsNestedUnderMainRow()
    {
        var records = new[]
        {
            Logged(1, At(22, 0), new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(60), ActivityReason.Block, 1, "b@1"), Kaori),
            Logged(2, At(22, 20), new ActionStartedPayload("post_sns", School, TimeSpan.FromMinutes(8), ActivityReason.Chosen, 1, null, true), Kaori),
            Logged(3, At(22, 28), new ActionEndedPayload("post_sns", 1, true), Kaori),
            Logged(4, At(22, 50), new ActionStartedPayload("post_sns", School, TimeSpan.FromMinutes(10), ActivityReason.Chosen, 1, null, true), Kaori),
            Logged(5, At(23, 0), new ActionEndedPayload("attend_class", 1), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-02（月）\n"
            + "香織\n"
            + "  07:00-08:00 授業を受ける＠2年A組の教室（予定）\n"
            + "    07:20-07:28 （スマホ）SNS に投稿する\n"
            + "    07:50-08:00 （スマホ）SNS に投稿する\n"
            + "\n"
            + "適用 5 件、捨てた 0 件、却下 0 件\n",
            text);
    }

    /// <summary>副行動の投稿のスキップは、副行動の字下げで、副行動の行の直後に出す。</summary>
    [Fact]
    public void SubPostSkipIsIndentedLikeItsSubAction()
    {
        var records = new[]
        {
            Logged(1, At(22, 0), new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(60), ActivityReason.Block, 1, "b@1"), Kaori),
            Logged(2, At(22, 20), new ActionStartedPayload("post_sns", School, TimeSpan.FromMinutes(8), ActivityReason.Chosen, 1, null, true), Kaori),
            Logged(3, At(22, 28), new ActionEndedPayload("post_sns", 1, true), Kaori),
            Logged(4, At(22, 28), new PostSkippedPayload(PostSkippedPayload.PostSkipReason.GenerationFailed, null), Kaori),
            Logged(5, At(23, 0), new ActionEndedPayload("attend_class", 1), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-02（月）\n"
            + "香織\n"
            + "  07:00-08:00 授業を受ける＠2年A組の教室（予定）\n"
            + "    07:20-07:28 （スマホ）SNS に投稿する\n"
            + "    07:28 （投稿をやめた：生成の失敗）\n"
            + "\n"
            + "適用 5 件、捨てた 0 件、却下 0 件\n",
            text);
    }

    /// <summary>投稿と投稿のスキップは、その時刻に1行の時点の行として出す（副行動の行とは別の行）。</summary>
    [Fact]
    public void PostingRowsAreShownAtTheirTime()
    {
        var records = new[]
        {
            Logged(1, At(22, 20), new ActionStartedPayload("post_sns", School, TimeSpan.FromMinutes(8), ActivityReason.Chosen, 1, null, true), Kaori),
            Logged(2, At(22, 28), new ActionEndedPayload("post_sns", 1, true), Kaori),
            Logged(3, At(22, 28), new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, "今日の授業眠い", null, Audience.Public, [], default, null), Kaori),
            Logged(4, At(22, 50), new PostSkippedPayload(PostSkippedPayload.PostSkipReason.AllRejected, null), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, Names, offsets);

        Assert.Equal(
            "2026-11-02（月）\n"
            + "香織\n"
            + "    07:20-07:28 （スマホ）SNS に投稿する\n"
            + "    07:28 （投稿）今日の授業眠い\n"
            + "  07:50 （投稿をやめた：話題がなかった）\n"
            + "\n"
            + "適用 4 件、捨てた 0 件、却下 0 件\n",
            text);
    }

    /// <summary>行動がひとつもなければ、表は空で件数だけを出す。</summary>
    [Fact]
    public void EmptyRecordsRenderOnlyCounts()
    {
        var text = DryRunReport.Render([], Names, new Dictionary<WorldId, TimeSpan>());

        Assert.Equal("適用 0 件、捨てた 0 件、却下 0 件\n", text);
    }

    /// <summary>2026-11-01 の、UTC の時刻を作る。</summary>
    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>適用の記録を作る。ワールドは固定の ID。</summary>
    private static LoggedEvent Logged(long seq, DateTimeOffset time, EventPayload payload, PersonaId persona, EventStatus status = EventStatus.Applied)
    {
        var type = payload switch
        {
            ActionStartedPayload => EventTypes.ActionStarted,
            ActionEndedPayload => EventTypes.ActionEnded,
            TravelStartedPayload => EventTypes.TravelStarted,
            TravelArrivedPayload => EventTypes.TravelArrived,
            OutboundSpeechPayload => EventTypes.OutboundSpeech,
            PostSkippedPayload => EventTypes.PostSkipped,
            _ => throw new ArgumentOutOfRangeException(nameof(payload)),
        };
        var ev = new SimEvent(seq, time, type, Town, persona, payload, null, null, null, Origin.Test, null, null);
        return new LoggedEvent(ev, status, status == EventStatus.Applied ? seq : null, null);
    }
}
