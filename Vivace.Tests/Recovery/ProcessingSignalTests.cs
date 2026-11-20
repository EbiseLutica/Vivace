using Vivace.Core;
using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Recovery;
using Vivace.Core.Routes;
using Vivace.Core.Signals;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Recovery;

/// <summary>処理中のイベントから起動し直すシグナルの対応（テストの町の定義で）。</summary>
public class ProcessingSignalTests
{
    /// <summary>太郎。</summary>
    private static readonly PersonaId Taro = new("taro");

    /// <summary>テストの町の規則のコンテキスト。</summary>
    private static readonly RuleContext Ctx = Context();

    /// <summary>生成の処理中の行は、行にペルソナがなくても、定義 ID のペルソナの Planner を起こす。</summary>
    [Fact]
    public void PersonaCreatedRestartsPlannerOfThePersonaByDefinitionId()
    {
        var ev = new SimEvent(2, Scenario.Start, EventTypes.PersonaCreated, null, null, new PersonaCreatedPayload(new PersonaDefId("taro"), new PlaceId("taro_room")), null, null, null, Origin.Test, null, null);

        var signal = Assert.IsType<StartPlannerSignal>(ProcessingSignal.For(Ctx, ev));

        Assert.Equal(2L, signal.BatchSeq);
        Assert.Equal(Taro, signal.Persona);
        Assert.Equal(Scenario.Start, signal.Horizon);
    }

    /// <summary>プランの期限の処理中の行は、そのペルソナの Planner を、イベントの時刻で起こす。</summary>
    [Fact]
    public void PlanDueRestartsPlannerAtItsTime()
    {
        var time = Scenario.Start.AddHours(3);
        var ev = new SimEvent(7, time, EventTypes.PlanDue, null, Taro, new PlanDuePayload(1), null, null, null, Origin.Sim, null, null);

        var signal = Assert.IsType<StartPlannerSignal>(ProcessingSignal.For(Ctx, ev));

        Assert.Equal(7L, signal.BatchSeq);
        Assert.Equal(Taro, signal.Persona);
        Assert.Equal(time, signal.Horizon);
    }

    /// <summary>決定タイミングの処理中の行は、理由と関係するブロックを持つ Decider を起こす。</summary>
    [Fact]
    public void DecisionRequestedRestartsDeciderWithItsReason()
    {
        var time = Scenario.Start.AddMinutes(30);
        var ev = new SimEvent(9, time, EventTypes.DecisionRequested, null, Taro, new DecisionRequestedPayload(DecisionReason.Free, null), null, null, null, Origin.Sim, null, null);

        var signal = Assert.IsType<StartDeciderSignal>(ProcessingSignal.For(Ctx, ev));

        Assert.Equal(9L, signal.BatchSeq);
        Assert.Equal(DecisionReason.Free, signal.Reason);
        Assert.Equal(time, signal.Now);
    }

    /// <summary>テストの町の規則のコンテキスト。</summary>
    private static RuleContext Context()
    {
        var defs = TestWorld.LoadOrThrow();
        return new RuleContext(defs, RouteCache.Create(defs), TestHolidays.Instance);
    }
}
