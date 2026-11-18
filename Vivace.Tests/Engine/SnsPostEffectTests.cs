using Vivace.Core;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// 投稿の効果（sns.post）を、台本の LLM で通しに回す。スマホを開く機会から投稿までの列と、LLM が失敗したときの扱い。
/// </summary>
public class SnsPostEffectTests
{
    /// <summary>台本の文面。</summary>
    private const string Body = "今日の授業眠い";

    /// <summary>スマホを開いて投稿した行動の終わりに、同じペルソナの発話が残る。その前に、スマホを開く機会がある。</summary>
    [Fact]
    public async Task PhonePostLeavesSpeechAtItsEnd()
    {
        await using var engine = await Scenario.RunAsync(7, llm: new ScriptedLlm().Reply("post.compose", Body));

        var started = engine.Log.First(entry => entry.Status == EventStatus.Applied
            && entry.Event.Payload is ActionStartedPayload { IsSubAction: true, ActionId: "post_sns" });
        var persona = started.Event.PersonaId;
        var end = started.Event.Time + ((ActionStartedPayload)started.Event.Payload).Duration;

        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied
            && entry.Event.EventType == EventTypes.PhoneOpportunity
            && entry.Event.PersonaId == persona
            && entry.Event.Time <= started.Event.Time);

        var speech = engine.Log.First(entry => entry.Status == EventStatus.Applied
            && entry.Event.Payload is OutboundSpeechPayload payload && payload.BasisSeqs.Contains(started.Event.Seq));
        var payload = (OutboundSpeechPayload)speech.Event.Payload;
        Assert.Equal(persona, speech.Event.PersonaId);
        Assert.Equal(end, speech.Event.Time);
        Assert.Equal(Body, payload.Body);
    }

    /// <summary>投稿は、最後の投稿の時刻に残る（発話の Apply）。</summary>
    [Fact]
    public async Task SpeechIsRecordedAsTheLastPost()
    {
        await using var engine = await Scenario.RunAsync(2, llm: new ScriptedLlm().Reply("post.compose", Body));

        var lastSpeech = engine.Log.Last(entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.OutboundSpeech);
        var posting = engine.State.Personas[lastSpeech.Event.PersonaId!.Value].Posting;

        Assert.True(posting.LastPostAt >= lastSpeech.Event.Time);
        Assert.Contains(posting.Recent, post => post.Body == Body);
    }

    /// <summary>LLM が例外で失敗しても、エンジンは止まらず、投稿はスキップとして残る。</summary>
    [Fact]
    public async Task FailedGenerationIsSkippedAndTheRunContinues()
    {
        await using var engine = await Scenario.RunAsync(7, llm: new ScriptedLlm().Fail("post.compose", new TimeoutException("わざと遅れた")));

        Assert.DoesNotContain(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.OutboundSpeech);
        var skipped = engine.Log.Where(entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.PostSkipped).ToList();
        Assert.NotEmpty(skipped);
        Assert.All(skipped, entry => Assert.Equal(PostSkippedPayload.PostSkipReason.GenerationFailed, ((PostSkippedPayload)entry.Event.Payload).Reason));
        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.ActionEnded
            && entry.Event.Time > Scenario.Start.AddDays(6));
    }

    /// <summary>台本に当たらない呼び出しも、例外として扱い、投稿をスキップにする（黙って空を返さない台本の既定）。</summary>
    [Fact]
    public async Task UnscriptedGenerationIsSkipped()
    {
        await using var engine = await Scenario.RunAsync(2, llm: new ScriptedLlm());

        Assert.DoesNotContain(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.OutboundSpeech);
        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied && entry.Event.EventType == EventTypes.PostSkipped);
    }
}
