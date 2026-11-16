using Vivace.Cli;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;

namespace Vivace.Tests.Cli;

/// <summary>流れる出力の1行。手で書いた小さな記録から、既定の4種の書式、時差での日付の繰り上がり、既定で出さないイベント、人のないイベントを確かめる。</summary>
public class EventLineFormatterTests
{
    private static readonly PersonaId Kaori = new("kaori");
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId School = new("school_2a");
    private static readonly WorldId Town = new("testville");
    private static readonly Dictionary<WorldId, TimeSpan> Offsets = new() { [Town] = TimeSpan.FromHours(9) };

    /// <summary>
    /// 行動と移動の開始と終わりは、現地の時刻の行になる。UTC 22:00 は、時差 +09:00 で翌日の 07:00。
    /// </summary>
    [Fact]
    public void DefaultModeShowsActionAndTravelStartAndEnd()
    {
        var started = new ActionStartedPayload("attend_class", School, TimeSpan.FromMinutes(90), ActivityReason.Block, 1, "b@1");
        var ended = new ActionEndedPayload("attend_class", 1);
        var travel = new TravelStartedPayload(KaoriRoom, School, [], At(23, 10), 2);
        var arrived = new TravelArrivedPayload(School, 2);

        Assert.Equal("11-02 07:00 kaori [予定] attend_class @school_2a", Format(Logged(At(22, 0), EventTypes.ActionStarted, started, Kaori)));
        Assert.Equal("11-02 07:30 kaori 終わり attend_class", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Kaori)));
        Assert.Equal("11-02 08:10 kaori 移動 kaori_room → school_2a", Format(Logged(At(23, 10), EventTypes.TravelStarted, travel, Kaori)));
        Assert.Equal("11-02 08:25 kaori 到着 school_2a", Format(Logged(At(23, 25), EventTypes.TravelArrived, arrived, Kaori)));
    }

    /// <summary>
    /// 時差 +09:00 で、日付が変わる境目。UTC 14:59 は現地の 11-01 23:59、UTC 15:00 は現地の 11-02 00:00。
    /// </summary>
    [Fact]
    public void LocalDateRollsOverAtLocalMidnight()
    {
        var ended = new ActionEndedPayload("read", 1);

        Assert.Equal("11-01 23:59 kaori 終わり read", Format(Logged(At(14, 59), EventTypes.ActionEnded, ended, Kaori)));
        Assert.Equal("11-02 00:00 kaori 終わり read", Format(Logged(At(15, 0), EventTypes.ActionEnded, ended, Kaori)));
    }

    /// <summary>既定では、行動と移動の4種以外は出さず、null を返す。</summary>
    [Fact]
    public void DefaultModeSkipsOtherEvents()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("kaori"), KaoriRoom);

        Assert.Null(Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Kaori)));
    }

    /// <summary>--all では、行動と移動の4種は既定と同じ書式、それ以外はイベントタイプの名前だけになる。</summary>
    [Fact]
    public void AllModePrintsEventTypeNameForOtherEvents()
    {
        var created = new PersonaCreatedPayload(new PersonaDefId("kaori"), KaoriRoom);
        var ended = new ActionEndedPayload("read", 1);

        Assert.Equal("11-02 07:00 kaori persona.created", Format(Logged(At(22, 0), EventTypes.PersonaCreated, created, Kaori), all: true));
        Assert.Equal("11-02 07:30 kaori 終わり read", Format(Logged(At(22, 30), EventTypes.ActionEnded, ended, Kaori), all: true));
    }

    /// <summary>人のない記録は、人の欄にワールド ID を書く。ワールドもなければ "-"、そのときの時刻は UTC。</summary>
    [Fact]
    public void EventWithoutPersonaShowsWorldOrDash()
    {
        var day = new DayStartedPayload(new DateOnly(2026, 11, 1));

        Assert.Equal("11-02 07:00 testville day.started", Format(Logged(At(22, 0), EventTypes.DayStarted, day, null, Town), all: true));
        Assert.Equal("11-01 22:00 - day.started", Format(Logged(At(22, 0), EventTypes.DayStarted, day, null, null), all: true));
    }

    /// <summary>2026-11-01 の、UTC の時刻を作る。</summary>
    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 11, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>テスト用の時差で1行にする。</summary>
    private static string? Format(LoggedEvent logged, bool all = false) =>
        EventLineFormatter.Format(logged, Offsets, all);

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
