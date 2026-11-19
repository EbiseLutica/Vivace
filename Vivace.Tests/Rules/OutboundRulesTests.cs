using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Signals;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>発話から未送信の操作を作り、送信の完了と失敗で外す規則。</summary>
public class OutboundRulesTests
{
    /// <summary>発話を当てると、未送信の操作が積まれ、配信のシグナルが出る。</summary>
    [Fact]
    public void SpeechQueuesAnOperationAndSignalsDelivery()
    {
        var result = Fixture.Apply(Fixture.WithTaro(), Speech(10, "今日の授業眠い"));

        Assert.Equal(EventStatus.Applied, result.Status);
        var operation = Assert.Single(result.State.Personas[Fixture.Taro].Outbound);
        Assert.Equal(10, operation.Seq);
        Assert.Equal("console", operation.Channel);
        Assert.Equal("post", operation.OperationKind);
        Assert.True(operation.IsSpeech);
        Assert.Equal("今日の授業眠い", operation.Payload.GetProperty("body").GetString());
        var signal = Assert.IsType<DeliverSignal>(Assert.Single(result.Signals));
        Assert.Equal(Fixture.Taro, signal.Persona);
        Assert.Equal(operation, signal.Operation);
    }

    /// <summary>送信の完了を当てると、未送信の操作が外れる。</summary>
    [Fact]
    public void SentRemovesTheOperation()
    {
        var spoken = Fixture.Apply(Fixture.WithTaro(), Speech(10, "今日の授業眠い")).State;

        var result = Fixture.Apply(spoken, Sent(11, 10));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Empty(result.State.Personas[Fixture.Taro].Outbound);
    }

    /// <summary>送信の失敗を当てると、未送信の操作が外れ、次に投稿できる時刻が残る。</summary>
    [Fact]
    public void FailedRemovesTheOperationAndKeepsTheRetryTime()
    {
        var spoken = Fixture.Apply(Fixture.WithTaro(), Speech(10, "今日の授業眠い")).State;
        var retryAfter = Fixture.Now.AddMinutes(20);

        var result = Fixture.Apply(spoken, Failed(11, 10, retryAfter));

        Assert.Equal(EventStatus.Applied, result.Status);
        var persona = result.State.Personas[Fixture.Taro];
        Assert.Empty(persona.Outbound);
        Assert.Equal(retryAfter, persona.Posting.RemoteNotBefore);
    }

    /// <summary>次に投稿できる時刻がない失敗は、投稿の状態を変えない。</summary>
    [Fact]
    public void FailedWithoutRetryTimeKeepsThePostingState()
    {
        var spoken = Fixture.Apply(Fixture.WithTaro(), Speech(10, "今日の授業眠い")).State;

        var result = Fixture.Apply(spoken, Failed(11, 10, null));

        Assert.Null(result.State.Personas[Fixture.Taro].Posting.RemoteNotBefore);
    }

    /// <summary>未送信の操作がない送信の完了は、適用しない（捨てた、の理由を残す）。</summary>
    [Fact]
    public void SentWithoutOperationIsSkipped()
    {
        var result = Fixture.Apply(Fixture.WithTaro(), Sent(11, 10));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Equal("未送信の操作がない", result.Reason);
    }

    /// <summary>未送信の操作がない送信の完了は、投入の検査で拒否する。</summary>
    [Fact]
    public void SentInjectionIsRejectedWithoutOperation()
    {
        var rule = EventApplier.Table[EventTypes.OutboundSent];

        var reason = rule.CheckInjection(Fixture.Context, Fixture.WithTaro(), [], new OutboundSentPayload(10, null), Fixture.Now);

        Assert.Equal("未送信の操作がない", reason);
    }

    /// <summary>発話のイベント（連番は seq、ペルソナは太郎）。</summary>
    private static SimEvent Speech(long seq, string body) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.OutboundSpeech,
            new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, body, null, Audience.Public, [], default, null),
            Fixture.Taro);

    /// <summary>送信の完了のイベント。</summary>
    private static SimEvent Sent(long seq, long operation) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.OutboundSent, new OutboundSentPayload(operation, "ext-1"), null);

    /// <summary>送信の失敗のイベント。</summary>
    private static SimEvent Failed(long seq, long operation, DateTimeOffset? retryAfter) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.OutboundFailed, new OutboundFailedPayload(operation, "接続できなかった", retryAfter), null);
}
