using Vivace.Core;
using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Llm;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// 投稿の途中の割り込み。投稿の行動の途中で睡眠が始まると、出力は残らず、投稿の記録も変わらない。
/// </summary>
public class SnsPostInterruptTests
{
    /// <summary>投稿の文面。</summary>
    private const string Body = "今日の授業眠い";

    /// <summary>香織。</summary>
    private static readonly PersonaId Kaori = new("kaori");

    /// <summary>
    /// 自由時間は、必ず10分の投稿を選ぶ（投稿が途切れずに続くように）。スマホを開く機会は、通常どおり決める。
    /// </summary>
    private sealed class PostingDecider : IDecider
    {
        /// <summary>スマホを開く機会の決定。</summary>
        private readonly UtilityDecider _phone = new();

        /// <summary>自由時間は投稿を選び、スマホを開く機会は通常の決定に任せる。</summary>
        public DeciderOutput Decide(DecisionContext context)
        {
            if (context.Reason == DecisionReason.Phone)
            {
                return _phone.Decide(context);
            }

            var persona = context.State.Personas[context.Persona];
            var payload = new ActivityChosenPayload("post_sns", UtilityDecider.DecisionPlace(persona), [], 0, TimeSpan.FromMinutes(10), false, null);
            return new DeciderOutput(payload, new Guard([new ActivityVersionCheck(persona.ActivityVersion)], RejectAction.Redecide));
        }
    }

    /// <summary>応答の前に少し待つ LLM。待っている間に、同じ時刻の割り込みを適用させる。</summary>
    private sealed class SlowLlm(ILlmClient inner) : ILlmClient
    {
        /// <summary>待ってから、中の LLM に任せる。</summary>
        /// <param name="request">呼び出しの内容。</param>
        /// <param name="ct">キャンセル。</param>
        public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
            return await inner.GenerateTextAsync(request, ct);
        }
    }

    /// <summary>
    /// 香織の投稿（23:40 から 10 分）の途中で、睡眠（23:45）が始まる。投稿の出力（発話もスキップも）は適用されず、
    /// 最後の投稿は前の投稿（23:40）のまま、最後のスキップも変わらない。
    /// </summary>
    [Fact]
    public async Task SleepInterruptingAPostLeavesNoOutputAndNoRecord()
    {
        var llm = new SlowLlm(new ScriptedLlm().Reply("post.compose", Body));
        await using var engine = await Scenario.CreateAsync(decider: new PostingDecider(), llm: llm);

        await engine.RunUntilAsync(Scenario.Start.AddMinutes(50), CancellationToken.None);

        var interrupted = Assert.Single(engine.Log, entry => entry.Status == EventStatus.Applied
            && entry.Event.PersonaId == Kaori
            && entry.Event.Time == Scenario.Start.AddMinutes(40)
            && entry.Event.Payload is ActionStartedPayload { ActionId: "post_sns", IsSubAction: false });
        Assert.Contains(engine.Log, entry => entry.Status == EventStatus.Applied
            && entry.Event.PersonaId == Kaori
            && entry.Event.Time == Scenario.Start.AddMinutes(45)
            && entry.Event.Payload is ActionStartedPayload { ActionId: "core.sleep" });

        var seq = interrupted.Event.Seq;
        Assert.DoesNotContain(engine.Log, entry => entry.Event.CauseSeq == seq
            && (entry.Event.EventType == EventTypes.OutboundSpeech || entry.Event.EventType == EventTypes.PostSkipped));

        var posting = engine.State.Personas[Kaori].Posting;
        Assert.Equal(Scenario.Start.AddMinutes(40), posting.LastPostAt);
        Assert.Null(posting.LastSkippedAt);
    }
}
