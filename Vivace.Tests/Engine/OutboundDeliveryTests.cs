using Vivace.Core;
using Vivace.Core.Channels;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Time;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Llm;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// 発話の配信。チャンネルに渡して、送信の完了か失敗を残す。ドライランでは送らず、未送信の操作が残る。
/// </summary>
public class OutboundDeliveryTests
{
    /// <summary>台本の文面。</summary>
    private const string Body = "今日の授業眠い";

    /// <summary>送った本文を確かめられる、送れたチャンネル。</summary>
    [Fact]
    public async Task EverySpeechIsSentWithItsBodyAndRecordedAsSent()
    {
        var channel = new RecordingChannel(Scenario.ChannelKind);
        await using var engine = await Scenario.RunAsync(2, llm: Posts(), channels: [channel]);

        var speeches = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundSpeech).Select(entry => entry.Event.Seq).ToList();
        var sent = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundSent)
            .Select(entry => ((OutboundSentPayload)entry.Event.Payload).OperationSeq).ToList();

        Assert.NotEmpty(speeches);
        Assert.Equal(speeches.Order(), sent.Order());
        Assert.Equal(speeches.Count, channel.Sent.Count);
        Assert.All(channel.Sent, request => Assert.Equal(Body, request.Payload.GetProperty("body").GetString()));
        Assert.All(engine.State.Personas.Values, persona => Assert.Empty(persona.Outbound));
    }

    /// <summary>送信が失敗すると、outbound.failed が残り、未送信の操作は外れる。送れたという記録はない。</summary>
    [Fact]
    public async Task FailedSendLeavesOutboundFailed()
    {
        var channel = new RecordingChannel(Scenario.ChannelKind, _ => new SendResult.Failed("接続できなかった", SurelyNotSent: true, RetryAfter: null));
        await using var engine = await Scenario.RunAsync(2, llm: Posts(), channels: [channel]);

        var speeches = Applied(engine).Count(entry => entry.Event.EventType == EventTypes.OutboundSpeech);
        var failed = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundFailed).ToList();

        Assert.NotEmpty(failed);
        Assert.Equal(speeches, failed.Count);
        Assert.All(failed, entry => Assert.Equal("接続できなかった", ((OutboundFailedPayload)entry.Event.Payload).Reason));
        Assert.DoesNotContain(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.OutboundSent);
        Assert.All(engine.State.Personas.Values, persona => Assert.Empty(persona.Outbound));
    }

    /// <summary>つないでいないチャンネルへの送信は、送っていない失敗になる。</summary>
    [Fact]
    public async Task SpeechWithoutChannelFails()
    {
        await using var engine = await Scenario.RunAsync(2, llm: Posts(), channels: []);

        var failed = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundFailed).Select(entry => (OutboundFailedPayload)entry.Event.Payload).ToList();

        Assert.NotEmpty(failed);
        Assert.All(failed, payload => Assert.Contains(Scenario.ChannelKind, payload.Reason, StringComparison.Ordinal));
    }

    /// <summary>結果を返さないチャンネルと、例外を投げるチャンネルは、どちらも失敗の結果になり、エンジンは進む。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("throw")]
    public async Task BrokenChannelResultBecomesFailedAndRunContinues(string mode)
    {
        var channel = new FakeChannel(Scenario.ChannelKind, (_, _) => mode == "null"
            ? Task.FromResult<SendResult>(null!)
            : Task.FromException<SendResult>(new InvalidOperationException("壊れた")));
        await using var engine = await Scenario.RunAsync(2, llm: Posts(), channels: [channel]);

        var speeches = Applied(engine).Count(entry => entry.Event.EventType == EventTypes.OutboundSpeech);
        var failed = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundFailed).Select(entry => (OutboundFailedPayload)entry.Event.Payload).ToList();

        Assert.NotEqual(0, speeches);
        Assert.Equal(speeches, failed.Count);
        Assert.All(failed, payload => Assert.Contains(mode == "null" ? "結果を返さなかった" : "壊れた", payload.Reason, StringComparison.Ordinal));
    }

    /// <summary>返らないチャンネルは、時間切れの失敗になり、エンジンは進む。</summary>
    [Fact]
    public async Task HangingChannelTimesOutAsFailedAndRunContinues()
    {
        var channel = new FakeChannel(Scenario.ChannelKind, (_, _) => new TaskCompletionSource<SendResult>().Task);
        await using var engine = await Scenario.RunAsync(2, llm: Posts(), channels: [channel], sendTimeout: TimeSpan.FromMilliseconds(20));

        var speeches = Applied(engine).Count(entry => entry.Event.EventType == EventTypes.OutboundSpeech);
        var failed = Applied(engine).Where(entry => entry.Event.EventType == EventTypes.OutboundFailed).Select(entry => (OutboundFailedPayload)entry.Event.Payload).ToList();

        Assert.NotEqual(0, speeches);
        Assert.Equal(speeches, failed.Count);
        Assert.All(failed, payload => Assert.Contains("時間切れ", payload.Reason, StringComparison.Ordinal));
    }

    /// <summary>ドライランでは送らず、発話の未送信の操作が残る（送信の完了も失敗もない）。</summary>
    [Fact]
    public async Task DryRunKeepsSpeechesUnsent()
    {
        var channel = new RecordingChannel(Scenario.ChannelKind);
        var storage = new InMemoryStorage();
        await using var engine = VivaceEngine.Create()
            .UseDefinitions(TestWorld.LoadOrThrow())
            .UseStorage(storage)
            .UseClock(new VirtualClock(Scenario.Start))
            .UseHolidays(TestHolidays.Instance)
            .UseLlm(Posts())
            .AddChannel(channel)
            .DryRun()
            .Build();
        await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test), CancellationToken.None);
        await engine.InjectAsync(InjectRequest.PersonaCreated(new PersonaDefId("taro"), new PlaceId("taro_room"), Origin.Test), CancellationToken.None);

        await engine.RunUntilAsync(Scenario.Start.AddDays(2), CancellationToken.None);

        var speeches = storage.Events.Count(entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.OutboundSpeech);
        Assert.True(speeches > 0);
        Assert.Empty(channel.Sent);
        Assert.DoesNotContain(storage.Events, entry => entry.Event.EventType is EventTypes.OutboundSent or EventTypes.OutboundFailed);
        Assert.Equal(speeches, storage.Latest!.Personas.Values.Sum(persona => persona.Outbound.Count));
    }

    /// <summary>投稿の文面は、どの呼び出しでも同じ一言を返す台本。</summary>
    private static ScriptedLlm Posts() => new ScriptedLlm().Reply("post.compose", Body);

    /// <summary>適用されたイベントの記録。</summary>
    private static IEnumerable<LoggedEvent> Applied(SimEngine engine) => engine.Log.Where(entry => entry.Status == EventStatus.Applied);
}
