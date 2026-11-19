using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Storage.Sqlite;
using Vivace.Tests.Serialization;

namespace Vivace.Tests.Storage;

/// <summary>
/// すべての列を埋めた、手で作った小さな状態と記録を往復する。書き込みの列が1つでも欠ければ、ここで落ちる。
/// 時刻は秒の単位の値（ミリ秒で保存できる値）にする。
/// </summary>
public class SqliteFullStateTests
{
    /// <summary>例の基準の時刻（UTC）。</summary>
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);

    /// <summary>例で使う場所（家）。</summary>
    private static readonly PlaceId Home = new("home");

    /// <summary>例で使う場所（喫茶店）。</summary>
    private static readonly PlaceId Cafe = new("cafe");

    /// <summary>例で使うワールド。</summary>
    private static readonly WorldId World = new("testville-1");

    /// <summary>例で使うペルソナ（移動中、副行動や決定の処理中を持つ）。</summary>
    private static readonly PersonaId Taro = new("taro");

    /// <summary>例で使うペルソナ（居場所だけを持つ）。</summary>
    private static readonly PersonaId Hanako = new("hanako");

    /// <summary>手で作った状態と記録を書いて開き直すと、記録と状態が書く前と一致する。</summary>
    [Fact]
    public async Task FullStateAndLogReadBackEqual()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        var log = FullLog();
        var state = FullState();
        var expectedEvents = log.Select(RoundTripDump.EventLine).ToArray();
        var expectedState = RoundTripDump.StateLines(state);

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        await using (var storage = new SqliteStorage(database))
        {
            await storage.CommitAsync(new StorageBatch(log, state), ct);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var restored = new SqliteStorage(reopened);
        var events = await restored.ReadEventsAsync(ct);
        var loaded = await restored.LoadStateAsync(ct);

        Assert.Equal(expectedEvents, events.Select(RoundTripDump.EventLine).ToArray());
        Assert.NotNull(loaded);
        Assert.Equal(expectedState, RoundTripDump.StateLines(loaded));
    }

    /// <summary>すべての列を使う状態。太郎は移動中で、副行動、決定の処理中、受信、未送信の操作を持つ。花子は居場所だけ。</summary>
    private static SimState FullState()
    {
        var home = new RuntimePlace(Home, "家", ["room"], null, [], PlaceStatus.Adopted, 2, 5);
        var cafe = new RuntimePlace(Cafe, "喫茶店", ["food"], "town", [new PlaceLink(Home, 5)], PlaceStatus.Candidate, 3, 4);
        var term = new TermEntry("まじ", TermVerdict.Convert, "本当", "slang", 6);
        var incident = new ActiveIncident(3, "rain", Taro, T0, null, Effects());
        var world = new WorldState(
            World,
            new WorldDefId("testville"),
            1234UL,
            WorldStatus.Alive,
            ImmutableDictionary<PlaceId, RuntimePlace>.Empty.Add(Home, home).Add(Cafe, cafe),
            ImmutableDictionary<string, TermEntry>.Empty.Add(term.Term, term),
            ImmutableDictionary<long, ActiveIncident>.Empty.Add(incident.Seq, incident),
            3);

        var vitals = new Vitals(new VitalTrack(T0, 80, -1.5), new VitalTrack(T0, 60, 0.5), new VitalTrack(T0, 10, 2), new VitalTrack(T0, 40, -0.25), 7);
        var item = new InboxItem(
            7,
            "misskey",
            InboxKind.Message,
            "misskey",
            new Sender("u1", "花子", "hanako"),
            Audience.Public,
            "conv-1",
            "こんにちは",
            [new Attachment("image", "https://example.invalid/a.png", null)],
            [new ThreadMessage(new Sender("u2", "太郎", null), true, "やあ", T0, [])],
            "like",
            "note-1",
            T0,
            PayloadSamples.Json("{\"id\":\"n1\"}"));
        var taro = new PersonaState(
            Taro,
            new PersonaDefId("taro"),
            World,
            PersonaStatus.Alive,
            new Traveling(Home, Cafe, [new RouteStep(Home, Cafe, 5)], T0.AddMinutes(5), T0.AddMinutes(10), new PendingAction("read", Cafe, TimeSpan.FromMinutes(10), ActivityReason.Block, "b1")),
            new Activity("read", Home, T0, T0.AddMinutes(30), ActivityReason.Chosen, "b1"),
            4,
            new Activity("phone", Home, T0, T0.AddMinutes(2), ActivityReason.Reflex, null),
            vitals,
            [new AttachedCondition("sleepy", 3, T0.AddHours(1))],
            Plan(),
            2,
            ImmutableDictionary<string, ActionUsage>.Empty.Add("read", new ActionUsage(T0, new DateOnly(2026, 11, 1), 2, 1)),
            ImmutableDictionary<string, ulong>.Empty.Add("decide", 42UL),
            new PostingState(T0, T0.AddMinutes(1), T0.AddMinutes(20), ImmutableList.Create(new RecentPost(T0, "眠い"))),
            ImmutableList.Create(item),
            ImmutableList.Create(new OutboundOp(8, "misskey", "reply", true, PayloadSamples.Json("{\"body\":\"hi\"}"))),
            T0,
            true,
            "b1",
            "k1");
        var hanako = new PersonaState(
            Hanako,
            new PersonaDefId("hanako"),
            World,
            PersonaStatus.Gap,
            new AtPlace(Home),
            null,
            1,
            null,
            vitals,
            [],
            null,
            0,
            ImmutableDictionary<string, ActionUsage>.Empty,
            ImmutableDictionary<string, ulong>.Empty,
            new PostingState(null, null, null, ImmutableList<RecentPost>.Empty),
            ImmutableList<InboxItem>.Empty,
            ImmutableList<OutboundOp>.Empty,
            null);

        return new SimState(
            new ApplyCursor(T0.AddSeconds(3), EventStage.World, 3, 2),
            ImmutableDictionary<WorldId, WorldState>.Empty.Add(World, world),
            ImmutableDictionary<PersonaId, PersonaState>.Empty.Add(Taro, taro).Add(Hanako, hanako));
    }

    /// <summary>適用の記録が2件と、捨てた記録が1件。ガード、積んだ元の連番、痕跡、実時刻を持つ。</summary>
    private static ImmutableArray<LoggedEvent> FullLog()
    {
        var planned = new SimEvent(
            1,
            T0,
            EventTypes.PlanCommitted,
            World,
            Taro,
            new PlanCommittedPayload(Plan()),
            new Guard([new PlanVersionCheck(1), new RngCursorCheck("taro", "decide", 3)], RejectAction.Redecide),
            4,
            2,
            Origin.FromChannel("misskey"),
            PayloadSamples.Json("{\"why\":\"x\"}"),
            T0.AddSeconds(1));
        var stale = new SimEvent(
            2,
            T0.AddSeconds(2),
            EventTypes.ActionStarted,
            World,
            Taro,
            new ActionStartedPayload("read", Home, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 4, "b1"),
            null,
            null,
            null,
            Origin.Sim,
            null,
            null);
        var incident = new SimEvent(
            3,
            T0.AddSeconds(3),
            EventTypes.IncidentOccurred,
            World,
            Taro,
            new IncidentOccurredPayload("rain", Taro, TimeSpan.FromMinutes(20), Effects()),
            null,
            null,
            1,
            Origin.Test,
            null,
            null);

        return
        [
            new LoggedEvent(planned, EventStatus.Applied, 1, null),
            new LoggedEvent(stale, EventStatus.Skipped, null, "古い"),
            new LoggedEvent(incident, EventStatus.Applied, 2, null),
        ];
    }

    /// <summary>出来事の効果（移動の遅さ、閉鎖、体の変化、状態を全部持つ）。</summary>
    private static ResolvedIncidentEffects Effects() =>
        new(
            new ResolvedSlowPlace(Home, TimeSpan.FromMinutes(5), 1.5),
            Cafe,
            ImmutableDictionary<VitalName, double>.Empty.Add(VitalName.Energy, -2.5),
            "wet");

    /// <summary>1日のプラン（ブロックを1つ持つ）。</summary>
    private static DayPlan Plan() =>
        new(
            2,
            T0,
            T0.AddDays(1),
            T0.AddHours(6),
            PlanSource.Llm,
            [new PlanBlock("b1", BlockKind.Fixed, T0, T0.AddHours(1), "sleep", Home, PhonePolicy.Closed, TimeSpan.FromMinutes(15), ["friend"], PartnerAbsentPolicy.Cancel)],
            "寝る");
}
