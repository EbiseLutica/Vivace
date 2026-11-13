using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>エンジンの投入の窓口。走っていないときはその場で、走っている間はループの中で処理する。</summary>
public class InjectAsyncTests
{
    private static readonly (PersonaDefId, PlaceId)[] KaoriOnly = [(new PersonaDefId("kaori"), new PlaceId("kaori_room"))];
    private static readonly PersonaDefId ShioriDef = new("shiori");
    private static readonly PlaceId HomeLiving = new("home_living");

    /// <summary>
    /// 走っていないときの投入は、その場で連番を返す。ワールドと香織で 1 と 2 を使ったので、栞は 3。
    /// 拒否した要求は連番を使わない。
    /// </summary>
    [Fact]
    public async Task InjectOutsideRunAssignsNextSeqAndRejectionUsesNone()
    {
        await using var engine = await Scenario.CreateAsync(personas: KaoriOnly);

        var rejected = await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("nobody"), HomeLiving, Origin.Test), CancellationToken.None);
        var accepted = await engine.InjectAsync(InjectRequest.PersonaCreated(ShioriDef, HomeLiving, Origin.Admin), CancellationToken.None);

        Assert.Equal(InjectResult.Reject("定義がない"), rejected);
        Assert.False(rejected.Accepted);
        Assert.Equal(InjectResult.Accept(3), accepted);
        Assert.True(accepted.Accepted);
    }

    /// <summary>走っていないときの過去の時刻は、拒否する。時計はシナリオの開始にある。</summary>
    [Fact]
    public async Task InjectOutsideRunRejectsPastTime()
    {
        await using var engine = await Scenario.CreateAsync(personas: KaoriOnly);

        var result = await engine.InjectAsync(InjectRequest.PersonaCreated(ShioriDef, HomeLiving, Origin.Test, Scenario.Start.AddSeconds(-1)), CancellationToken.None);

        Assert.Equal(InjectResult.Reject("過去の時刻"), result);
    }

    /// <summary>
    /// 走っている間（決める側の仕事の出力を待っている間）の投入は、ループが起きて処理し、仕事が終わる前に結果を返す。
    /// 時刻を省略した栞の生成は、受け付けた時点の時刻（投入の前に見た適用の位置より後）で適用され、出所も残る。
    /// </summary>
    [Fact]
    public async Task InjectWhileRunningIsAppliedAtOrAfterInjectionTime()
    {
        var decider = new BlockingDecider();
        await using var engine = await Scenario.CreateAsync(decider: decider, personas: KaoriOnly);
        Task run;
        InjectResult result;
        DateTimeOffset before;
        try
        {
            run = engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);
            await decider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            before = engine.State.Cursor.Time;

            result = await engine.InjectAsync(InjectRequest.PersonaCreated(ShioriDef, HomeLiving, Origin.Admin), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.False(run.IsCompleted);
        }
        finally
        {
            decider.Release.Set();
        }

        await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(result.Accepted, result.Reason);
        var logged = Assert.Single(engine.Log, entry => entry.Event.Seq == result.Seq);
        Assert.Equal(EventStatus.Applied, logged.Status);
        Assert.Equal(EventTypes.PersonaCreated, logged.Event.EventType);
        Assert.Equal(Origin.Admin, logged.Event.Origin);
        Assert.True(logged.Event.Time >= before, $"投入の前の位置 {before:O} より前に適用された: {logged.Event.Time:O}");
        Assert.Same(engine.State, engine.View);
        Assert.True(engine.View.Personas.ContainsKey(new PersonaId("shiori")));
    }

    /// <summary>走っている間に、もう一度 RunUntilAsync を呼ぶと、例外になる。</summary>
    [Fact]
    public async Task RunUntilWhileRunningThrows()
    {
        var decider = new BlockingDecider();
        await using var engine = await Scenario.CreateAsync(decider: decider, personas: KaoriOnly);
        Task run;
        try
        {
            run = engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);
            await decider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunUntilAsync(Scenario.Start.AddDays(2), CancellationToken.None));
        }
        finally
        {
            decider.Release.Set();
        }

        await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }
}
