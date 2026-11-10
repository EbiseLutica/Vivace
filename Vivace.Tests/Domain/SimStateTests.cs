using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Domain;

/// <summary>SimState のペルソナの取り出しと、居合わせの判定を、ペルソナの状態を差し替えた小さな例で確かめる。</summary>
public class SimStateTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");

    /// <summary>ペルソナの状態を変えた全体の状態。</summary>
    private static SimState Edit(SimState state, PersonaId id, Func<PersonaState, PersonaState> change) =>
        state.WithPersona(change(Fixture.Get(state, id)));

    /// <summary>ID がなければ null。</summary>
    [Fact]
    public void AlivePersonaIsNullWithoutId()
    {
        Assert.Null(Fixture.WithKaori().AlivePersona(null));
    }

    /// <summary>ID に当たるペルソナがなければ null。</summary>
    [Fact]
    public void AlivePersonaIsNullForUnknownId()
    {
        Assert.Null(Fixture.WithKaori().AlivePersona(new PersonaId("nobody")));
    }

    /// <summary>生きていないペルソナは、取り出せない（記録としては残るが、規則の対象にしない）。</summary>
    [Fact]
    public void AlivePersonaIsNullWhenNotAlive()
    {
        var state = Edit(Fixture.WithKaori(), Fixture.Kaori, persona => persona with { Status = PersonaStatus.Destroyed });

        Assert.Null(state.AlivePersona(Fixture.Kaori));
    }

    /// <summary>生きているペルソナは、そのまま取り出せる。</summary>
    [Fact]
    public void AlivePersonaReturnsLivingPersona()
    {
        var state = Fixture.WithKaori();

        Assert.Equal(state.Personas[Fixture.Kaori], state.AlivePersona(Fixture.Kaori));
    }

    /// <summary>同じ場所に生きていない人がいても、居合わせではない。</summary>
    [Fact]
    public void AnyoneElseAtIgnoresNotAlive()
    {
        var state = Fixture.WithBoth();
        state = Edit(state, Fixture.Shiori, persona => persona with { Location = new AtPlace(KaoriRoom), Status = PersonaStatus.Destroyed });

        Assert.False(state.AnyoneElseAt(KaoriRoom, Fixture.Kaori));
    }

    /// <summary>移動中の人は、どの場所にもいないものとして扱うので、居合わせではない。</summary>
    [Fact]
    public void AnyoneElseAtIgnoresTraveling()
    {
        var state = Fixture.WithBoth();
        state = Edit(state, Fixture.Shiori, persona => persona with
        {
            Location = new Traveling(KaoriRoom, HomeLiving, ImmutableArray<RouteStep>.Empty, Fixture.Now, Fixture.Now.AddHours(1)),
        });

        Assert.False(state.AnyoneElseAt(KaoriRoom, Fixture.Kaori));
    }

    /// <summary>自分だけが場所にいるときは、居合わせではない。自分は数えない。</summary>
    [Fact]
    public void AnyoneElseAtDoesNotCountSelf()
    {
        Assert.False(Fixture.WithKaori().AnyoneElseAt(KaoriRoom, Fixture.Kaori));
    }

    /// <summary>生きている別の人が同じ場所にいれば、居合わせである。</summary>
    [Fact]
    public void AnyoneElseAtFindsLivingPersonInPlace()
    {
        var state = Edit(Fixture.WithBoth(), Fixture.Shiori, persona => persona with { Location = new AtPlace(KaoriRoom) });

        Assert.True(state.AnyoneElseAt(KaoriRoom, Fixture.Kaori));
        Assert.False(state.AnyoneElseAt(HomeLiving, Fixture.Kaori));
    }

    /// <summary>移動中のペルソナは、同じ場所に人がいても、居合わせではない。いまの場所で数えるときの規則。</summary>
    [Fact]
    public void IsTogetherIsFalseWhileTraveling()
    {
        var state = Edit(Fixture.WithBoth(), Fixture.Shiori, persona => persona with { Location = new AtPlace(KaoriRoom) });
        var traveling = Edit(state, Fixture.Kaori, persona => persona with
        {
            Location = new Traveling(KaoriRoom, HomeLiving, ImmutableArray<RouteStep>.Empty, Fixture.Now, Fixture.Now.AddHours(1)),
        });

        Assert.True(state.IsTogether(Fixture.Get(state, Fixture.Kaori)));
        Assert.False(traveling.IsTogether(Fixture.Get(traveling, Fixture.Kaori)));
    }

    /// <summary>定義 ID に当たる生きているワールドは、そのまま取り出せる。</summary>
    [Fact]
    public void AliveWorldReturnsLivingWorldOfDefinition()
    {
        var state = Fixture.WithWorld();

        Assert.Equal(state.Worlds[Fixture.World], state.AliveWorld(TestWorld.DefId));
    }

    /// <summary>定義 ID に当たるワールドがなければ null。</summary>
    [Fact]
    public void AliveWorldIsNullForUnknownDefinition()
    {
        Assert.Null(Fixture.WithWorld().AliveWorld(new WorldDefId("nowhere")));
    }

    /// <summary>定義 ID に当たるワールドが破棄されていれば null。</summary>
    [Fact]
    public void AliveWorldIsNullWhenDestroyed()
    {
        var state = Fixture.WithWorld();
        var destroyed = state.Worlds[Fixture.World] with { Status = WorldStatus.Destroyed };
        state = state with { Worlds = state.Worlds.SetItem(Fixture.World, destroyed) };

        Assert.Null(state.AliveWorld(TestWorld.DefId));
    }

    /// <summary>破棄された testville と、生きている testville#2（定義は testville）があれば、生きている testville#2 を返す。</summary>
    [Fact]
    public void AliveWorldSkipsDestroyedInstance()
    {
        var state = Fixture.WithWorld();
        var destroyed = state.Worlds[Fixture.World] with { Status = WorldStatus.Destroyed };
        var alive = destroyed with { Id = new WorldId("testville#2"), Status = WorldStatus.Alive };
        state = state with { Worlds = state.Worlds.SetItem(Fixture.World, destroyed).SetItem(alive.Id, alive) };

        Assert.Equal(new WorldId("testville#2"), state.AliveWorld(TestWorld.DefId)!.Id);
    }
}
