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
    private static readonly PersonaId Taro = new("taro");
    private static readonly PlaceId TaroRoom = new("taro_room");
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
        var travel = new TravelStartedPayload(TaroRoom, School, [], At(23, 10), 2);
        var arrived = new TravelArrivedPayload(School, 2);

        Assert.Equal("11-02 07:00 太郎は2年A組の教室で「授業を受ける」（予定）", Format(Logged(At(22, 0), EventTypes.ActionStarted, started, Taro)));
        Assert.Equal("11-02 07:30 太郎は「授業を受ける」を終えた", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Taro)));
        Assert.Equal("11-02 08:10 太郎は太郎の部屋から2年A組の教室へ向かった", Format(Logged(At(23, 10), EventTypes.TravelStarted, travel, Taro)));
        Assert.Equal("11-02 08:25 太郎は2年A組の教室に着いた", Format(Logged(At(23, 25), EventTypes.TravelArrived, arrived, Taro)));
    }

    /// <summary>
    /// 時差 +09:00 で、日付が変わる境目。UTC 14:59 は現地の 11-01 23:59、UTC 15:00 は現地の 11-02 00:00。
    /// </summary>
    [Fact]
    public void LocalDateRollsOverAtLocalMidnight()
    {
        var ended = new ActionEndedPayload("read_book", 1);

        Assert.Equal("11-01 23:59 太郎は「本を読む」を終えた", Format(Logged(At(14, 59), EventTypes.ActionEnded, ended, Taro)));
        Assert.Equal("11-02 00:00 太郎は「本を読む」を終えた", Format(Logged(At(15, 0), EventTypes.ActionEnded, ended, Taro)));
    }

    /// <summary>副行動（スマホを開く、投稿する）の開始と終わりは、「（スマホ）」を付けた文になり、主の行動の文と見分けられる。UTC 22:20 は現地の 07:20。</summary>
    [Fact]
    public void SubActionLinesAreMarkedWithPhone()
    {
        var started = new ActionStartedPayload("post_sns", TaroRoom, TimeSpan.FromMinutes(8), ActivityReason.Chosen, 1, null, true);
        var ended = new ActionEndedPayload("post_sns", 1, true);

        Assert.Equal("11-02 07:20 太郎は（スマホ）「SNS に投稿する」を始めた", Format(Logged(At(22, 20), EventTypes.ActionStarted, started, Taro)));
        Assert.Equal("11-02 07:28 太郎は（スマホ）「SNS に投稿する」を終えた", Format(Logged(At(22, 28), EventTypes.ActionEnded, ended, Taro)));
    }

    /// <summary>既定では、行動と移動の4種以外は出さず、null を返す。</summary>
    [Fact]
    public void DefaultModeSkipsOtherEvents()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("taro"), TaroRoom);

        Assert.Null(Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Taro)));
    }

    /// <summary>--all では、人が読んで意味のあるイベントを文にする。文にできないものは、人の名前と括弧のイベントタイプにする。</summary>
    [Fact]
    public void AllModeWritesReadableSentencesAndTypeNamesForTheRest()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("taro"), TaroRoom);
        var other = new PersonaDestroyedPayload();

        Assert.Equal("11-02 07:00 太郎が太郎の部屋に現れた", Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Taro), all: true));
        Assert.Equal("11-02 07:00 太郎（persona.destroyed）", Format(Logged(At(22, 0), EventTypes.PersonaDestroyed, other, Taro), all: true));
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

        Assert.Equal("11-02 07:00 太郎の予定を立てた", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(first), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎の予定を立て直した", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(rebuilt), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎は固定予定だけで予定を立てた", Format(Logged(At(22, 0), EventTypes.PlanCommitted, new PlanCommittedPayload(fallback), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎の予定を立て直す時刻になった", Format(Logged(At(22, 0), EventTypes.PlanDue, new PlanDuePayload(1), Taro), all: true));
    }

    /// <summary>投稿は「（投稿）」の文、やめた投稿は理由つきの文になり、既定でも出す。</summary>
    [Fact]
    public void PostingLinesAreShownByDefault()
    {
        var speech = new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, "今日の授業眠い", null, Audience.Public, [], default, null);
        var skipped = new PostSkippedPayload(PostSkippedPayload.PostSkipReason.GenerationFailed, null);

        Assert.Equal("11-02 07:28 太郎（投稿）今日の授業眠い", Format(Logged(At(22, 28), EventTypes.OutboundSpeech, speech, Taro)));
        Assert.Equal("11-02 07:30 太郎（投稿をやめた：生成の失敗）", Format(Logged(At(22, 30), EventTypes.PostSkipped, skipped, Taro)));
    }

    /// <summary>--all でも、行動と移動は既定と同じ文になる。</summary>
    [Fact]
    public void AllModeKeepsActionSentence()
    {
        var ended = new ActionEndedPayload("read_book", 1);

        Assert.Equal("11-02 07:30 太郎は「本を読む」を終えた", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Taro), all: true));
    }

    /// <summary>--all の日と世界の文。日付は "11月1日（日）" の形、ワールドは定義の名前。</summary>
    [Fact]
    public void AllModeWritesDayAndWorldSentences()
    {
        var day = new DayStartedPayload(new DateOnly(2026, 11, 1));
        var world = new WorldCreatedPayload(new WorldDefId("testville"), 1);

        Assert.Equal("11-02 07:00 テストの町で11月1日（日）が始まった", Format(Logged(At(22, 0), EventTypes.DayStarted, day, null, Town), all: true));
        Assert.Equal("11-02 07:00 テストの町ができた", Format(Logged(At(22, 0), EventTypes.WorldCreated, world, null, null), all: true));
    }

    /// <summary>
    /// 記録にワールドがなくても、人の定義のワールドの時差で現地の時刻にする。UTC 22:00 の生成は、testville の +09:00 で 11-02 07:00。
    /// </summary>
    [Fact]
    public void RecordWithoutWorldUsesPersonaWorldOffset()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("taro"), TaroRoom);

        Assert.Equal("11-02 07:00 太郎が太郎の部屋に現れた", Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, null, null), all: true));
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
        Assert.Equal("11-02 07:00 太郎の予定「授業を受ける」が始まった", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("school@2026-11-02", 1), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎の予定「授業を受ける」が始まった", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("school.2@2026-11-02", 1), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎（block.started）", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload("no_such@2026-11-02", 1), Taro), all: true));
        Assert.Equal("11-02 07:00 太郎の自由な時間が始まった", Format(Logged(At(22, 0), EventTypes.BlockStarted, new BlockStartedPayload(null, 1), Taro), all: true));
    }

    /// <summary>名前のない行動の ID は、ID のまま文に入る（落ちない）。</summary>
    [Fact]
    public void UnknownActionIdStaysAsIs()
    {
        var started = new ActionStartedPayload("no_such", School, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 1, null);

        Assert.Equal("11-02 07:00 太郎は2年A組の教室で「no_such」（自由）", Format(Logged(At(22, 0), EventTypes.ActionStarted, started, Taro)));
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
