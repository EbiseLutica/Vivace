using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Injection;

/// <summary>投入の窓口の検査。共通の検査（時刻、型）と、ワールドとペルソナの生成の検査を確かめる。</summary>
public class InjectionValidatorTests
{
    private static readonly PersonaDefId KaoriDef = new("kaori");
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>処理する時点より前の時刻は、拒否する。</summary>
    [Fact]
    public void PastTimeIsRejected()
    {
        var request = InjectRequest.WorldCreated(TestWorld.DefId, 42, Origin.Test, Fixture.Now.AddMinutes(-1));

        var reason = Reject(Fixture.Empty(), [], request);

        Assert.Equal("過去の時刻", reason);
    }

    /// <summary>時刻を省略すると、処理する時点の時刻で、要求の出所のまま下書きになる。</summary>
    [Fact]
    public void OmittedTimeUsesNow()
    {
        var request = InjectRequest.WorldCreated(TestWorld.DefId, 42, Origin.Admin);

        var draft = Accept(Fixture.Empty(), [], request);

        Assert.Equal(Fixture.Now, draft.Time);
        Assert.Equal(EventTypes.WorldCreated, draft.EventType);
        Assert.Equal(new WorldCreatedPayload(TestWorld.DefId, 42), draft.Payload);
        Assert.Equal(Origin.Admin, draft.Origin);
        Assert.Null(draft.WorldId);
        Assert.Null(draft.PersonaId);
    }

    /// <summary>処理する時点と同じ時刻は、過去ではない。</summary>
    [Fact]
    public void SameTimeAsNowIsAccepted()
    {
        var draft = Accept(Fixture.Empty(), [], InjectRequest.WorldCreated(TestWorld.DefId, 42, Origin.Test, Fixture.Now));

        Assert.Equal(Fixture.Now, draft.Time);
    }

    /// <summary>ルールの表にない型は、未対応で拒否する。</summary>
    [Fact]
    public void TypeWithoutRuleIsRejected()
    {
        var request = new InjectRequest(EventTypes.VitalAdjusted, new DayStartedPayload(new DateOnly(2026, 11, 1)), null, Origin.Test);

        Assert.Equal("未対応の型", Reject(Fixture.WithKaori(), [], request));
    }

    /// <summary>ルールの表にあっても、投入の検査を持たない型（日の始まり）は、未対応で拒否する。</summary>
    [Fact]
    public void RuleWithoutInjectionCheckIsRejected()
    {
        var request = new InjectRequest(EventTypes.DayStarted, new DayStartedPayload(new DateOnly(2026, 11, 2)), null, Origin.Test);

        Assert.Equal("未対応の型", Reject(Fixture.WithWorld(), [], request));
    }

    /// <summary>型と中身が合わない要求は、拒否する。</summary>
    [Fact]
    public void MismatchedPayloadIsRejected()
    {
        var request = new InjectRequest(EventTypes.WorldCreated, new PersonaCreatedPayload(KaoriDef, KaoriRoom), null, Origin.Test);

        Assert.Equal("中身の型が違う", Reject(Fixture.Empty(), [], request));
    }

    /// <summary>ワールドの定義がない生成は、拒否する。</summary>
    [Fact]
    public void WorldWithoutDefinitionIsRejected()
    {
        var request = InjectRequest.WorldCreated(new WorldDefId("nowhere"), 42, Origin.Test);

        Assert.Equal("定義がない", Reject(Fixture.Empty(), [], request));
    }

    /// <summary>ペルソナの定義がない生成は、ワールドが生きていても拒否する。</summary>
    [Fact]
    public void PersonaWithoutDefinitionIsRejected()
    {
        var request = InjectRequest.PersonaCreated(new PersonaDefId("nobody"), KaoriRoom, Origin.Test);

        Assert.Equal("定義がない", Reject(Fixture.WithWorld(), [], request));
    }

    /// <summary>居住するワールドが生きていれば、ペルソナの生成を受け付ける。</summary>
    [Fact]
    public void PersonaInAliveWorldIsAccepted()
    {
        var draft = Accept(Fixture.WithWorld(), [], InjectRequest.PersonaCreated(KaoriDef, KaoriRoom, Origin.Test));

        Assert.Equal(EventTypes.PersonaCreated, draft.EventType);
        Assert.Equal(new PersonaCreatedPayload(KaoriDef, KaoriRoom), draft.Payload);
    }

    /// <summary>居住するワールドがなく、キューにも生成がなければ、拒否する。</summary>
    [Fact]
    public void PersonaWithoutWorldIsRejected()
    {
        Assert.Equal("ワールドがない", Reject(Fixture.Empty(), [], InjectRequest.PersonaCreated(KaoriDef, KaoriRoom, Origin.Test)));
    }

    /// <summary>居住するワールドの生成が、要求と同じ時刻でキューにあれば、生成済みとみなして受け付ける。</summary>
    [Fact]
    public void PersonaWithQueuedWorldAtSameTimeIsAccepted()
    {
        var queued = QueuedWorld(Fixture.Now, TestWorld.DefId);

        var draft = Accept(Fixture.Empty(), [queued], InjectRequest.PersonaCreated(KaoriDef, KaoriRoom, Origin.Test, Fixture.Now));

        Assert.Equal(Fixture.Now, draft.Time);
    }

    /// <summary>キューのワールドの生成が、要求より後の時刻なら、拒否する（ペルソナの方が先に適用されるため）。</summary>
    [Fact]
    public void PersonaWithQueuedWorldAfterRequestIsRejected()
    {
        var queued = QueuedWorld(Fixture.Now.AddMinutes(1), TestWorld.DefId);

        var reason = Reject(Fixture.Empty(), [queued], InjectRequest.PersonaCreated(KaoriDef, KaoriRoom, Origin.Test, Fixture.Now));

        Assert.Equal("ワールドがない", reason);
    }

    /// <summary>キューにあるのが別のワールドの生成なら、拒否する。</summary>
    [Fact]
    public void PersonaWithOtherQueuedWorldIsRejected()
    {
        var queued = QueuedWorld(Fixture.Now, new WorldDefId("elsewhere"));

        var reason = Reject(Fixture.Empty(), [queued], InjectRequest.PersonaCreated(KaoriDef, KaoriRoom, Origin.Test, Fixture.Now));

        Assert.Equal("ワールドがない", reason);
    }

    /// <summary>キューにあるワールドの生成のイベント。</summary>
    private static SimEvent QueuedWorld(DateTimeOffset time, WorldDefId defId) =>
        new(1, time, EventTypes.WorldCreated, null, null, new WorldCreatedPayload(defId, 42), null, null, null, Origin.Test, null, null);

    /// <summary>基準の時刻で検査し、通ることを確かめて下書きを返す。</summary>
    private static EventDraft Accept(SimState state, SimEvent[] pending, InjectRequest request)
    {
        var accepted = new InjectionValidator(Fixture.Context).TryValidate(state, pending, request, Fixture.Now, out var draft, out var reason);

        Assert.True(accepted, reason);
        return draft!;
    }

    /// <summary>基準の時刻で検査し、通らないことを確かめて理由を返す。</summary>
    private static string Reject(SimState state, SimEvent[] pending, InjectRequest request)
    {
        var accepted = new InjectionValidator(Fixture.Context).TryValidate(state, pending, request, Fixture.Now, out var draft, out var reason);

        Assert.False(accepted);
        Assert.Null(draft);
        return reason!;
    }
}
