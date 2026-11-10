using System.Collections.Immutable;
using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// エンジンを通した却下と、決める側の仕事の例外。どちらも黙って捨てず、記録に残るか、例外で止まる。
/// </summary>
public class EngineFailureTests
{
    /// <summary>決める側の仕事が例外を投げたら、エンジンは例外で止まる（黙って続けない）。</summary>
    [Fact]
    public async Task FailingDecisionStopsEngine()
    {
        await using var engine = Scenario.Create(Scenario.WorldSeed, new ThrowingDecider());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None));
        Assert.Contains("決める側の仕事が失敗", error.Message, StringComparison.Ordinal);
    }

    /// <summary>レーンの仕事が包みを抜けて例外を投げたら、出力を待っているエンジンも、待ちをやめて例外で止まる。破棄は投げずに終わる。</summary>
    [Fact]
    public async Task LaneFailureStopsWaitingEngine()
    {
        var decider = new BlockingDecider();
        var failure = new InvalidOperationException("わざと失敗させたレーンの仕事");
        await using var engine = Scenario.Create(Scenario.WorldSeed, decider);
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

    /// <summary>guard が通らない出力は、却下として記録され、決め直しで続く。</summary>
    [Fact]
    public async Task StaleDecisionIsRecordedAsRejectedAndRunContinues()
    {
        await using var engine = Scenario.Create(Scenario.WorldSeed, new RejectOnceDecider());

        await engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);

        var rejected = engine.Log.Where(entry => entry.Status == EventStatus.Rejected).ToList();
        var single = Assert.Single(rejected);
        Assert.Equal(EventTypes.ActivityChosen, single.Event.EventType);
        Assert.Equal("行動の版が違う", single.Reason);
        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.ActivityChosen);
    }

    private sealed class ThrowingDecider : IDecider
    {
        public DeciderOutput Decide(DecisionContext context) => throw new InvalidOperationException("わざと失敗させた決定");
    }

    /// <summary>最初の決定で、解放されるまで止まる Decider。</summary>
    private sealed class BlockingDecider : IDecider
    {
        private readonly UtilityDecider _inner = new();

        /// <summary>決定に入ったら完了する。</summary>
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>決定を先へ進める合図。</summary>
        public ManualResetEventSlim Release { get; } = new();

        public DeciderOutput Decide(DecisionContext context)
        {
            Entered.TrySetResult();
            Release.Wait();
            return _inner.Decide(context);
        }
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
