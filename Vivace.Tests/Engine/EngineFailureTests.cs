using System.Collections.Immutable;
using Vivace.Core.Decision;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// エンジンを通した却下と、決める側のジョブの例外。どちらも握りつぶさず、記録に残るか、例外で止まる。
/// </summary>
public class EngineFailureTests
{
    /// <summary>Decider が例外を投げたら、代わりの決定（core.go_to_next）になり、エンジンは止まらずに返る。</summary>
    [Fact]
    public async Task FailingDecisionFallsBackToGoToNextAndTheRunReturns()
    {
        await using var engine = await Scenario.CreateAsync(Scenario.WorldSeed, new ThrowingDecider());

        await engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var chosen = engine.Log
            .Where(entry => entry.Status == EventStatus.Applied && entry.Event.Payload is ActivityChosenPayload)
            .Select(entry => ((ActivityChosenPayload)entry.Event.Payload).ActionId)
            .ToList();
        Assert.Contains("core.go_to_next", chosen, StringComparer.Ordinal);
        Assert.All(chosen, id => Assert.Contains(id, new[] { "core.go_to_next", "core.idle" }));
    }

    /// <summary>レーンのジョブが包みを抜けて例外を投げたら、出力を待っているエンジンも、待ちをやめて例外で止まる。破棄は投げずに終わる。</summary>
    [Fact]
    public async Task LaneFailureStopsWaitingEngine()
    {
        var decider = new BlockingDecider();
        var failure = new InvalidOperationException("わざと失敗させたレーンのジョブ");
        await using var engine = await Scenario.CreateAsync(Scenario.WorldSeed, decider);
        try
        {
            var run = engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);
            await decider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            engine.Lanes.Post("world:test", () => throw failure);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(failure, error.InnerException);
        }
        finally
        {
            decider.Release.Set();
        }
    }

    /// <summary>決める側の出力を待っている間にキャンセルすると、待ちをやめて OperationCanceledException で抜ける。</summary>
    [Fact]
    public async Task CancelWhileWaitingForOutputStopsRun()
    {
        var decider = new BlockingDecider();
        await using var engine = await Scenario.CreateAsync(Scenario.WorldSeed, decider);
        using var cts = new CancellationTokenSource();
        try
        {
            var run = engine.RunUntilAsync(Scenario.Start.AddDays(1), cts.Token);
            await decider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        finally
        {
            decider.Release.Set();
        }
    }

    /// <summary>guard が通らない出力は、却下として記録され、再決定で続く。</summary>
    [Fact]
    public async Task StaleDecisionIsRecordedAsRejectedAndRunContinues()
    {
        await using var engine = await Scenario.CreateAsync(Scenario.WorldSeed, new RejectOnceDecider());

        await engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);

        var rejected = engine.Log.Where(entry => entry.Status == EventStatus.Rejected).ToList();
        var single = Assert.Single(rejected);
        Assert.Equal(EventTypes.ActivityChosen, single.Event.EventType);
        Assert.Equal("行動のバージョンが違う", single.Reason);
        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.ActivityChosen);
    }

    private sealed class RejectOnceDecider : IDecider
    {
        private readonly UtilityDecider _inner = new();
        private bool _rejected;

        public DeciderOutput Decide(DecisionContext context)
        {
            var output = _inner.Decide(context);
            if (_rejected)
            {
                return output;
            }

            _rejected = true;
            return output with { Guard = output.Guard with { Checks = ImmutableArray.Create<GuardCheck>(new ActivityVersionCheck(999)) } };
        }
    }
}
