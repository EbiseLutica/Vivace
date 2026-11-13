using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Routes;
using Vivace.Core;
using Vivace.Testing;

namespace Vivace.Tests.Support;

/// <summary>
/// 規則のテスト用の土台。テスト用の小さな町の定義と、ワールドとペルソナを1人ずつ生成した状態を作る。
/// 状態は、Apply で作る（テストの中で状態を手で組まない）。
/// </summary>
public static class Fixture
{
    /// <summary>テストの基準の時刻。2026-11-01 の 23:00（現地）。</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);

    /// <summary>テストのワールドの ID。</summary>
    public static readonly WorldId World = new(TestWorld.DefId.Value);

    /// <summary>香織のインスタンス ID。</summary>
    public static readonly PersonaId Kaori = new("kaori");

    /// <summary>栞のインスタンス ID。</summary>
    public static readonly PersonaId Shiori = new("shiori");

    private static readonly Lazy<RuleContext> Shared = new(() =>
    {
        var definitions = TestWorld.LoadOrThrow();
        return new RuleContext(definitions, RouteCache.Create(definitions));
    });

    /// <summary>テストの規則の文脈（定義と最短経路の表）。</summary>
    public static RuleContext Context => Shared.Value;

    /// <summary>空の状態。位置は基準の時刻。</summary>
    public static SimState Empty() => new(
        new Position(Now, Core.Events.EventStage.DayTurn, 0, 0),
        ImmutableDictionary<WorldId, WorldState>.Empty,
        ImmutableDictionary<PersonaId, PersonaState>.Empty);

    /// <summary>ワールドを作った状態。</summary>
    public static SimState WithWorld() =>
        Apply(Empty(), Event(1, Now, EventTypes.WorldCreated, new WorldCreatedPayload(TestWorld.DefId, 42), null)).State;

    /// <summary>ワールドと香織（家は kaori_room）を作った状態。</summary>
    public static SimState WithKaori() =>
        Apply(WithWorld(), Event(2, Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("kaori_room")), null)).State;

    /// <summary>ワールドと香織と栞（家は home_living）を作った状態。</summary>
    public static SimState WithBoth() =>
        Apply(WithKaori(), Event(3, Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("shiori"), new PlaceId("home_living")), null)).State;

    /// <summary>規則を1つ適用する。</summary>
    public static ApplyResult Apply(SimState state, SimEvent ev) => new EventApplier(Context).Apply(state, ev);

    /// <summary>適用に使うイベントを作る（テストの出所）。</summary>
    public static SimEvent Event(long seq, DateTimeOffset time, string type, EventPayload payload, PersonaId? persona, Guard? guard = null, long? batch = null) =>
        new(seq, time, type, World, persona, payload, guard, batch, null, Origin.Test, null, null);

    /// <summary>ペルソナの状態を差し替えた状態。</summary>
    public static SimState With(SimState state, PersonaState persona) => state.WithPersona(persona);

    /// <summary>ペルソナの状態を取り出す。</summary>
    public static PersonaState Get(SimState state, PersonaId id) => state.Personas[id];
}
