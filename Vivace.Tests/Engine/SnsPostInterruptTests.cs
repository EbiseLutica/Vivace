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

    /// <summary>
    /// 香織の 23:50 に出す投稿の文面だけ、門が開くまで応答しない LLM。ほかの呼び出しは中の LLM に任せる。
    /// 取り消されなかった呼び出しは、ホライズンでエンジンを待たせ続ける。
    /// </summary>
    private sealed class GatedLlm(ILlmClient inner) : ILlmClient
    {
        /// <summary>応答を止めている門。</summary>
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>止めていた応答を返させる。</summary>
        public void Open() => _gate.TrySetResult();

        /// <summary>止める呼び出しなら門が開くまで待ち、中の LLM に任せる。</summary>
        public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            if (request.System.Contains("人物：香織", StringComparison.Ordinal)
                && request.Messages.Any(message => message.Content.Contains("23:50", StringComparison.Ordinal)))
            {
                await _gate.Task;
            }

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
        var llm = new GatedLlm(new ScriptedLlm().Reply("post.compose", Body));
        await using var engine = await Scenario.CreateAsync(decider: new PostingDecider(), llm: llm);
        try
        {
            await engine.RunUntilAsync(Scenario.Start.AddMinutes(50), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            llm.Open();
        }

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

    /// <summary>
    /// 香織の 23:50 に出す投稿の文面を、取り消しを待つ LLM。呼ばれたら、取り消しのトークンを捕まえて、取り消されるまで待つ（取り消されなければ 10 秒で打ち切る）。
    /// </summary>
    private sealed class CancelWaitingLlm : ILlmClient
    {
        /// <summary>呼ばれたことを知らせる。</summary>
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>呼ばれたときのキャンセル。</summary>
        public CancellationToken Captured { get; private set; }

        /// <summary>呼ばれるまで待つ。</summary>
        public Task Entered => _entered.Task;

        /// <summary>対象の呼び出しなら、トークンを捕まえて取り消されるまで待つ。ほかの呼び出しはしない（取り消しを待つ呼び出しだけを見る）。</summary>
        public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            if (request.System.Contains("人物：香織", StringComparison.Ordinal)
                && request.Messages.Any(message => message.Content.Contains("23:50", StringComparison.Ordinal)))
            {
                Captured = ct;
                _entered.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }

            return new LlmResult<string>(Body, new LlmUsage("waiting", 0, 0));
        }
    }

    /// <summary>
    /// 投稿の途中で睡眠が始まると、投稿の文面の LLM 呼び出しのトークンが取り消される。出力は、取り消しの代わりの出力も残らない。
    /// </summary>
    [Fact]
    public async Task SleepInterruptingAPostCancelsTheLlmCall()
    {
        var llm = new CancelWaitingLlm();
        await using var engine = await Scenario.CreateAsync(decider: new PostingDecider(), llm: llm);
        await engine.RunUntilAsync(Scenario.Start.AddMinutes(50), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await llm.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(llm.Captured.IsCancellationRequested);

        var interrupted = Assert.Single(engine.Log, entry => entry.Status == EventStatus.Applied
            && entry.Event.PersonaId == Kaori
            && entry.Event.Time == Scenario.Start.AddMinutes(40)
            && entry.Event.Payload is ActionStartedPayload { ActionId: "post_sns", IsSubAction: false });
        var seq = interrupted.Event.Seq;
        Assert.DoesNotContain(engine.Log, entry => entry.Event.CauseSeq == seq
            && (entry.Event.EventType == EventTypes.OutboundSpeech || entry.Event.EventType == EventTypes.PostSkipped));
    }
}
