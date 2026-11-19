using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Signals;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>行動の効果のハンドラの起動。効果（handler）のある行動の開始だけが、終わりの時刻を出力の時刻にして起動する。</summary>
public class EffectStartRulesTests
{
    private static readonly PlaceId TaroRoom = new("taro_room");

    /// <summary>効果のない行動の開始は、シグナルを出さない。</summary>
    [Fact]
    public void ActionWithoutHandlerStartsNoEffect()
    {
        var result = Fixture.Apply(Fixture.WithTaro(), Started(10, "read_book", TimeSpan.FromMinutes(30), 0));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Empty(result.Signals);
    }

    /// <summary>投稿の行動の開始は、行動の終わりの時刻を出力の時刻にして、効果を起動する。</summary>
    [Fact]
    public void PostActionStartsEffectAtItsEnd()
    {
        var result = Fixture.Apply(Fixture.WithTaro(), Started(10, "post_sns", TimeSpan.FromMinutes(8), 0));

        var signal = Assert.IsType<StartEffectSignal>(Assert.Single(result.Signals));
        Assert.Equal(10, signal.BatchSeq);
        Assert.Equal(Fixture.Taro, signal.Persona);
        Assert.Equal(Fixture.Now.AddMinutes(8), signal.Horizon);
        Assert.Equal(0, signal.ActivityVersion);
    }

    /// <summary>副行動（スマホの投稿）も、効果を起動する。主の行動は変えない。</summary>
    [Fact]
    public void SubActionPostStartsEffectWithoutTouchingTheMainAction()
    {
        var main = Fixture.Apply(Fixture.WithTaro(), Started(10, "read_book", TimeSpan.FromMinutes(30), 0)).State;

        var result = Fixture.Apply(main, Started(11, "post_sns", TimeSpan.FromMinutes(8), 0, isSubAction: true));

        var signal = Assert.IsType<StartEffectSignal>(Assert.Single(result.Signals));
        Assert.Equal(11, signal.BatchSeq);
        Assert.Equal(Fixture.Now.AddMinutes(8), signal.Horizon);
        Assert.Equal(0, result.State.Personas[Fixture.Taro].ActivityVersion);
    }

    /// <summary>行動の開始のイベント。主の行動は行動のバージョンで、副行動は版を上げない。</summary>
    private static SimEvent Started(long seq, string actionId, TimeSpan duration, long version, bool isSubAction = false) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload(actionId, TaroRoom, duration, ActivityReason.Chosen, version, null, isSubAction), Fixture.Taro);
}
