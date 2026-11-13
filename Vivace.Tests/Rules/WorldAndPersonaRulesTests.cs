using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Signals;
using Vivace.Core;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Rules;

/// <summary>ワールドの生成、ペルソナの生成、日の始まり、状態変数の読み取りを、手で計算した値で確かめる。</summary>
public class WorldAndPersonaRulesTests
{
    /// <summary>ワールドを作ると、その日の日の始まりが積まれ、ワールドが生きている。</summary>
    [Fact]
    public void WorldCreatedAddsWorldAndSchedulesTodayStart()
    {
        var result = Fixture.Apply(Fixture.Empty(), Fixture.Event(1, Fixture.Now, EventTypes.WorldCreated, new WorldCreatedPayload(TestWorld.DefId, 42), null));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Equal(WorldStatus.Alive, result.State.Worlds[Fixture.World].Status);
        var followUp = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.DayStarted, followUp.EventType);
        Assert.Equal(new DayStartedPayload(new DateOnly(2026, 11, 1)), followUp.Payload);
    }

    /// <summary>同じワールドを二度作ると、捨てられる（状態は変わらない）。</summary>
    [Fact]
    public void WorldCreatedTwiceIsSkipped()
    {
        var state = Fixture.WithWorld();
        var result = Fixture.Apply(state, Fixture.Event(9, Fixture.Now, EventTypes.WorldCreated, new WorldCreatedPayload(TestWorld.DefId, 42), null));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Same(state, result.State);
    }

    /// <summary>日の始まりは、現地（+09:00）の翌日の0時を次の日の始まりとして積む。2026-11-02 の 0:00 は UTC の 15:00 の前日。</summary>
    [Fact]
    public void DayStartedSchedulesNextLocalMidnight()
    {
        var state = Fixture.WithWorld();
        var ev = Fixture.Event(5, Fixture.Now, EventTypes.DayStarted, new DayStartedPayload(new DateOnly(2026, 11, 1)), null);

        var result = Fixture.Apply(state, ev);

        var followUp = Assert.Single(result.FollowUps);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero), followUp.Time);
        Assert.Equal(new DayStartedPayload(new DateOnly(2026, 11, 2)), followUp.Payload);
    }

    /// <summary>ペルソナは家に生まれ、定義の初期値で始まる。率は気力 −4、退屈度 +3、社交欲求は一人のとき +2。</summary>
    [Fact]
    public void PersonaCreatedStartsAtHomeWithInitialVitals()
    {
        var result = Fixture.Apply(Fixture.WithWorld(), Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("kaori_room")), null));

        var persona = result.State.Personas[Fixture.Kaori];
        Assert.Equal(new AtPlace(new PlaceId("kaori_room")), persona.Location);
        Assert.Equal(80, persona.Vitals.Energy.BaseValue);
        Assert.Equal(-4.0, persona.Vitals.Energy.Rate);
        Assert.Equal(100, persona.Vitals.Health.BaseValue);
        Assert.Equal(30, persona.Vitals.Boredom.BaseValue);
        Assert.Equal(3.0, persona.Vitals.Boredom.Rate);
        Assert.Equal(0, persona.Vitals.Social.BaseValue);
        Assert.Equal(2.0, persona.Vitals.Social.Rate);
        var signal = Assert.Single(result.Signals);
        Assert.Equal(new StartPlannerSignal(2, Fixture.Kaori, Fixture.Now), signal);
    }

    /// <summary>家の場所がないペルソナは作らない。</summary>
    [Fact]
    public void PersonaCreatedWithUnknownHomeIsSkipped()
    {
        var result = Fixture.Apply(Fixture.WithWorld(), Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("nowhere")), null));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Empty(result.State.Personas);
    }

    /// <summary>
    /// 同じ定義の testville が破棄されていて、生きている testville#2 があれば、ペルソナは testville#2 に生まれる。イベントの WorldId（testville）は使わない。
    /// </summary>
    [Fact]
    public void PersonaCreatedUsesLivingWorldInstanceOfHomeDefinition()
    {
        var state = Fixture.WithWorld();
        var destroyed = state.Worlds[Fixture.World] with { Status = WorldStatus.Destroyed };
        var alive = destroyed with { Id = new WorldId("testville#2"), Status = WorldStatus.Alive };
        state = state with { Worlds = state.Worlds.SetItem(Fixture.World, destroyed).SetItem(alive.Id, alive) };

        var result = Fixture.Apply(state, Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("kaori_room")), null));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Equal(new WorldId("testville#2"), result.State.Personas[Fixture.Kaori].World);
    }

    /// <summary>生きているワールドのインスタンスがなければ、ペルソナは作らない。</summary>
    [Fact]
    public void PersonaCreatedWithoutLivingWorldIsSkipped()
    {
        var state = Fixture.WithWorld();
        var destroyed = state.Worlds[Fixture.World] with { Status = WorldStatus.Destroyed };
        state = state with { Worlds = state.Worlds.SetItem(Fixture.World, destroyed) };

        var result = Fixture.Apply(state, Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("kaori_room")), null));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Empty(result.State.Personas);
    }

    /// <summary>同じ場所に人がいると、社交欲求の率は同席の率（−4）になる。出ていくと、両方とも一人の率（+2）に戻る。</summary>
    [Fact]
    public void SocialRateFollowsCoPresence()
    {
        var kaoriHome = Fixture.WithKaori();
        var result = Fixture.Apply(kaoriHome, Fixture.Event(3, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("shiori"), new PlaceId("kaori_room")), null));

        Assert.Equal(-4.0, result.State.Personas[Fixture.Kaori].Vitals.Social.Rate);
        Assert.Equal(-3.0, result.State.Personas[Fixture.Shiori].Vitals.Social.Rate);
        Assert.Equal(2.0, Fixture.WithBoth().Personas[Fixture.Kaori].Vitals.Social.Rate);
    }

    /// <summary>
    /// <summary>範囲に切り詰めた読み取り。気力は 0 で止まり、社交欲求は下限と上限の間に収まる。</summary>
    [Fact]
    public void ReadClampsToRange()
    {
        var definitions = TestWorld.LoadOrThrow();
        var kaori = definitions.Personas[new PersonaDefId("kaori")].Vitals;
        var energy = new VitalTrack(Fixture.Now, 80, -4);
        var social = new VitalTrack(Fixture.Now, 0, 2);

        Assert.Equal(0, energy.ValueAt(Fixture.Now.AddHours(30), (0, 100)));
        Assert.Equal(60, social.ValueAt(Fixture.Now.AddHours(100), VitalRules.Range(VitalName.Social, kaori)));
        Assert.Equal(56, energy.ValueAt(Fixture.Now.AddHours(6), (0, 100)));
    }
}
