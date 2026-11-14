using Vivace.Cli;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;

namespace Vivace.Tests.Cli;

/// <summary>行動表の組み立て。手で書いた小さな記録から、行の形、閉じ方、日と人の並び、件数を確かめる。</summary>
public class DryRunReportTests
{
    private static readonly PersonaId Kaori = new("kaori");
    private static readonly PersonaId Shiori = new("shiori");
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");
    private static readonly PlaceId School = new("school");
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
            Logged(1, At(22, 30), new ActionStartedPayload("read", HomeLiving, TimeSpan.FromMinutes(20), ActivityReason.Chosen, 1, null), Shiori),
            Logged(2, At(22, 0), new ActionStartedPayload("eat_breakfast", KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Block, 1, "b@1"), Kaori),
            Logged(3, At(22, 30), new ActionEndedPayload("eat_breakfast", 1), Kaori),
            Logged(4, At(23, 10), new TravelStartedPayload(KaoriRoom, School, [], At(23, 25), 2), Kaori),
            Logged(5, At(23, 12), new ActionEndedPayload("read", 1), Kaori, EventStatus.Skipped),
            Logged(6, At(23, 25), new TravelArrivedPayload(School, 2), Kaori),
            Logged(7, At(23, 25), new ActionStartedPayload("study", School, TimeSpan.FromMinutes(60), ActivityReason.Chosen, 3, null), Kaori),
            Logged(8, At(22, 50), new ActionEndedPayload("read", 1), Shiori),
            Logged(9, At(23, 40), new TravelStartedPayload(KaoriRoom, HomeLiving, [], At(23, 55), 4), Kaori, EventStatus.Rejected),
            Logged(10, At(23, 13), new ActionEndedPayload("study", 1), Kaori, EventStatus.Skipped),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, offsets);

        Assert.Equal(
            "2026-11-02（月）\n"
            + "kaori\n"
            + "  07:00-07:30 [予定] eat_breakfast @kaori_room\n"
            + "  08:10-08:25 移動 kaori_room → school\n"
            + "  08:25-未終了 [自由] study @school\n"
            + "shiori\n"
            + "  07:30-07:50 [自由] read @home_living\n"
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
            Logged(1, At(1, 0), new ActionStartedPayload("read", HomeLiving, TimeSpan.FromMinutes(30), ActivityReason.Reflex, 1, null), Kaori),
            Logged(2, At(1, 30), new ActionStartedPayload("study", School, TimeSpan.FromMinutes(30), ActivityReason.Late, 2, null), Kaori),
            Logged(3, At(2, 0), new TravelStartedPayload(School, KaoriRoom, [], At(2, 20), 3), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, offsets);

        Assert.Equal(
            "2026-11-01（日）\n"
            + "kaori\n"
            + "  10:00-10:30 [反射] read @home_living\n"
            + "  10:30-11:00 [遅刻] study @school\n"
            + "  11:00-未終了 移動 school → kaori_room\n"
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
            Logged(4, At(2, 0), new ActionStartedPayload("read", HomeLiving, TimeSpan.FromMinutes(20), ActivityReason.Chosen, 2, null), Kaori),
        };
        var offsets = new Dictionary<WorldId, TimeSpan> { [Town] = TimeSpan.FromHours(9) };

        var text = DryRunReport.Render(records, offsets);

        Assert.Equal(
            "2026-11-01（日）\n"
            + "kaori\n"
            + "  11:00-未終了 [自由] read @home_living\n"
            + "\n"
            + "適用 1 件、捨てた 2 件、却下 1 件\n",
            text);
    }

    /// <summary>行動がひとつもなければ、表は空で件数だけを出す。</summary>
    [Fact]
    public void EmptyRecordsRenderOnlyCounts()
    {
        var text = DryRunReport.Render([], new Dictionary<WorldId, TimeSpan>());

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
            _ => throw new ArgumentOutOfRangeException(nameof(payload)),
        };
        var ev = new SimEvent(seq, time, type, Town, persona, payload, null, null, null, Origin.Test, null, null);
        return new LoggedEvent(ev, status, status == EventStatus.Applied ? seq : null, null);
    }
}
