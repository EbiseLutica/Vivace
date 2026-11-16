using Vivace.Cli;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Cli;

/// <summary>流れる出力の1行。手で書いた小さな記録と、テスト用の定義の名前から、既定の文、時差での日付の繰り上がり、既定で出さないイベント、--all の文、名前のないものの ID を確かめる。</summary>
public class EventLineFormatterTests
{
    private static readonly DisplayNames Names = new(TestWorld.LoadOrThrow());
    private static readonly PersonaId Kaori = new("kaori");
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId School = new("school_2a");
    private static readonly WorldId Town = new("testville");
    private static readonly Dictionary<WorldId, TimeSpan> Offsets = new() { [Town] = TimeSpan.FromHours(9) };

    /// <summary>
    /// 行動と移動の開始と終わりは、現地の時刻の文になる。UTC 22:00 は、時差 +09:00 で翌日の 07:00。
    /// </summary>
    [Fact]
    public void DefaultModeShowsActionAndTravelStartAndEnd()
    {
        var started = new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(90), ActivityReason.Block, 1, "b@1");
        var ended = new ActionEndedPayload("attend_class", 1);
        var travel = new TravelStartedPayload(KaoriRoom, School, [], At(23, 10), 2);
        var arrived = new TravelArrivedPayload(School, 2);

        Assert.Equal("11-02 07:00 香織は2年A組の教室で「授業を受ける」（予定）", Format(Logged(At(22, 0), EventTypes.ActionStarted, started, Kaori)));
        Assert.Equal("11-02 07:30 香織は「授業を受ける」を終えた", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Kaori)));
        Assert.Equal("11-02 08:10 香織は香織の部屋から2年A組の教室へ向かった", Format(Logged(At(23, 10), EventTypes.TravelStarted, travel, Kaori)));
        Assert.Equal("11-02 08:25 香織は2年A組の教室に着いた", Format(Logged(At(23, 25), EventTypes.TravelArrived, arrived, Kaori)));
    }

    /// <summary>
    /// 時差 +09:00 で、日付が変わる境目。UTC 14:59 は現地の 11-01 23:59、UTC 15:00 は現地の 11-02 00:00。
    /// </summary>
    [Fact]
    public void LocalDateRollsOverAtLocalMidnight()
    {
        var ended = new ActionEndedPayload("read_book", 1);

        Assert.Equal("11-01 23:59 香織は「本を読む」を終えた", Format(Logged(At(14, 59), EventTypes.ActionEnded, ended, Kaori)));
        Assert.Equal("11-02 00:00 香織は「本を読む」を終えた", Format(Logged(At(15, 0), EventTypes.ActionEnded, ended, Kaori)));
    }

    /// <summary>既定では、行動と移動の4種以外は出さず、null を返す。</summary>
    [Fact]
    public void DefaultModeSkipsOtherEvents()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("kaori"), KaoriRoom);

        Assert.Null(Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Kaori)));
    }

    /// <summary>--all では、人が読んで意味のあるイベントを文にする。文にできないものは、人の名前と括弧のイベントタイプにする。</summary>
    [Fact]
    public void AllModeWritesReadableSentencesAndTypeNamesForTheRest()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("kaori"), KaoriRoom);
        var other = new PersonaDestroyedPayload();

        Assert.Equal("11-02 07:00 香織が香織の部屋に現れた", Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Kaori), all: true));
        Assert.Equal("11-02 07:00 香織（persona.destroyed）", Format(Logged(At(22, 0), EventTypes.PersonaDestroyed, other, Kaori), all: true));
    }

    /// <summary>
    /// プランの確定は、版が1なら「立てた」、2以上は「立て直した」。固定予定だけのプランは「固定予定だけで」と書く。作り直しの時刻は「立て直す時刻」。
    /// </summary>
    [Fact]
    public void PlanCommittedAndDueSentences()
    {
        var first = new DayPlan(1, At(22, 0), At(23, 0), At(23, 0), PlanSource.Llm, [], null);
        var rebuilt = first with { Version = 2 };
        var fallback = first with { Source = PlanSource.Fallback };

        Assert.Equal("11-02 07:00 香織の予定を立てた", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(first), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織の予定を立て直した", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(rebuilt), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織は固定予定だけで予定を立てた", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(fallback), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織の予定を立て直す時刻になった", Format(Logged(At(22, 0), EventTypes.PlanDue, new PlanDuePayload(1), Kaori), all: true));
    }

    /// <summary>--all でも、行動と移動は既定と同じ文になる。</summary>
    [Fact]
    public void AllModeKeepsActionSentence()
    {
        var ended = new ActionEndedPayload("read_book", 1);

        Assert.Equal("11-02 07:30 香織は「本を読む」を終えた", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Kaori), all: true));
    }

    /// <summary>--all の日と世界の文。日付は "11月01日（日）" の形、ワールドは定義の名前。</summary>
    [Fact]
    public void AllModeWritesDayAndWorldSentences()
    {
        var day = new DayStartedPayload(new DateOnly(2026, 11, 1));
        var world = new WorldCreatedPayload(new WorldDefId("testville"), 1);

        Assert.Equal("11-02 07:00 テストの町で 11月1日（日）が始まった", Format(Logged(At(22, 0), EventTypes.DayStarted, day, null, Town), all: true));
        Assert.Equal("11-02 07:00 テストの町ができた", Format(Logged(At(22, 0), EventTypes.WorldCreated, world, null, null), all: true));
    }

    /// <summary>
    /// 記録にワールドがなくても、人の定義のワールドの時差で現地の時刻にする。UTC 22:00 の生成は、testville の +09:00 で 11-02 07:00。
    /// </summary>
    [Fact]
    public void RecordWithoutWorldUsesPersonaWorldOffset()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("kaori"), KaoriRoom);

        Assert.Equal("11-02 07:00 香織が香織の部屋に現れた", Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, null, null), all: true));
    }

    /// <summary>人もワールドもない記録は、括弧のイベントタイプだけ。そのときの時刻は UTC。</summary>
    [Fact]
    public void EventWithoutPersonaOrWorldShowsTypeOnly()
    {
        var day = new DayStartedPayload(new DateOnly(2026, 11, 1));

        Assert.Equal("11-01 22:00 （day.started）", Format(Logged(At(22, 0), EventTypes.DayStarted, day, null, null), all: true));
    }

    /// <summary>
    /// 固定予定のブロックの開始は、ブロックの行動の名前で「予定」の文にする。同じ日の2回目は ".2" が付くが同じ行動。引けなければ括弧のイベントタイプ。
    /// </summary>
    [Fact]
    public void BlockStartedNamesTheBlocksActivity()
    {
        Assert.Equal("11-02 07:00 香織の予定「授業を受ける」が始まった", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("school@2026-11-02", 1), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織の予定「授業を受ける」が始まった", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("school.2@2026-11-02", 1), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織（block.started）", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("no_such@2026-11-02", 1), Kaori), all: true));
        Assert.Equal("11-02 07:00 香織（block.started）", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload(null, 1), Kaori), all: true));
    }

    /// <summary>名前のない行動の ID は、ID のまま文に入る（落ちない）。</summary>
    [Fact]
    public void UnknownActionIdStaysAsIs()
    {
        var started = new ActionStartedPayload("no_such", School, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 1, null);

        Assert.Equal("11-02 07:00 香織は2年A組の教室で「no_such」（自由）", Format(Logged(At(22, 0), EventTypes.ActionStarted, started, Kaori)));
    }

    /// <summary>2026-11-01 の、UTC の時刻を作る。</summary>
    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>テスト用の時差と名前で1行にする。</summary>
    private static string? Format(LoggedEvent logged, bool all = false) =>
        EventLineFormatter.Format(logged, Names, Offsets, all);

    /// <summary>ワールドが testville の適用の記録を作る。</summary>
    private static LoggedEvent Logged(DateTimeOffset time, string type, EventPayload payload, PersonaId? persona) =>
        Logged(time, type, payload, persona, Town);

    /// <summary>適用の記録を作る。</summary>
    private static LoggedEvent Logged(DateTimeOffset time, string type, EventPayload payload, PersonaId? persona, WorldId? world)
    {
        var ev = new SimEvent(1, time, type, world, persona, payload, null, null, null, Origin.Test, null, null);
        return new LoggedEvent(ev, EventStatus.Applied, 1, null);
    }
}
