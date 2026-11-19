using System.Globalization;
using System.Text.Json;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Llm;
using Vivace.Core.Signals;
using Vivace.Core.Time;

namespace Vivace.Core.Handlers;

/// <summary>
/// 投稿の効果（sns.post）。ペルソナの声で独り言を1つ作る。空の本文はスキップにする。例外は、エンジンが代わりの出力にする。
/// </summary>
internal static class SnsPostHandler
{
    /// <summary>効果の名前（行動の effects.handler）。</summary>
    public const string Name = "sns.post";

    /// <summary>LLM の目的。</summary>
    public const string Purpose = "post.compose";

    /// <summary>チャンネルの中身。コンソールは追加の情報を持たない。</summary>
    private static readonly JsonElement EmptyChannelPayload = JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// 独り言を1つ作り、出力のイベントを返す。出力の時刻は行動の終わり。行動のバージョンが変わっていたら、guard で捨てられる。
    /// </summary>
    /// <param name="ctx">規則のコンテキスト（定義とルート）。</param>
    /// <param name="llm">LLM。</param>
    /// <param name="snapshot">起動の時点の状態。</param>
    /// <param name="effect">起動のシグナル。</param>
    /// <param name="ct">取り消し。取り消されたら、LLM の呼び出しを止め、例外は代わりの出力にする。</param>
    public static async Task<EventDraft> RunAsync(RuleContext ctx, ILlmClient llm, SimState snapshot, StartEffectSignal effect, CancellationToken ct)
    {
        var persona = snapshot.Personas[effect.Persona];
        var world = ctx.Defs.Worlds[snapshot.Worlds[persona.World].DefId];
        var personaDef = ctx.Defs.Personas[persona.DefId];
        if (personaDef.Channels.IsDefaultOrEmpty)
        {
            return Skipped(snapshot, effect, PostSkippedPayload.PostSkipReason.NoChannel);
        }

        var channel = personaDef.Channels[0];
        var action = ctx.Defs.Actions[effect.ActionId];
        var result = await llm.GenerateTextAsync(Request(personaDef, world, persona, action, effect), ct);
        var body = result.Value.Trim();
        if (body.Length == 0)
        {
            return Skipped(snapshot, effect);
        }

        var guard = new Guard([new ActivityVersionCheck(effect.ActivityVersion)], RejectAction.Drop);
        var speech = new OutboundSpeechPayload(channel, OutboundSpeechPayload.OutboundPostKind.Post, body, null, Audience.Public, [effect.BatchSeq], EmptyChannelPayload, null);
        return new EventDraft(effect.Horizon, EventTypes.OutboundSpeech, persona.World, persona.Id, speech, guard, effect.BatchSeq);
    }

    /// <summary>
    /// 投稿をスキップする出力。省略すると生成の失敗。ハンドラが失敗したときの代わりの出力にも使う。
    /// </summary>
    /// <param name="snapshot">起動の時点の状態。</param>
    /// <param name="effect">起動のシグナル。</param>
    /// <param name="reason">スキップの理由。</param>
    public static EventDraft Skipped(SimState snapshot, StartEffectSignal effect, PostSkippedPayload.PostSkipReason reason = PostSkippedPayload.PostSkipReason.GenerationFailed)
    {
        var persona = snapshot.Personas[effect.Persona];
        var guard = new Guard([new ActivityVersionCheck(effect.ActivityVersion)], RejectAction.Drop);
        var payload = new PostSkippedPayload(reason, null);
        return new EventDraft(effect.Horizon, EventTypes.PostSkipped, persona.World, persona.Id, payload, guard, effect.BatchSeq);
    }

    /// <summary>
    /// 文面の呼び出し。システムの指示にはペルソナと世界の lore、メッセージには時刻、行動、場所、直近の投稿を置く。
    /// </summary>
    private static LlmRequest Request(PersonaDef personaDef, WorldDef world, PersonaState persona, ActionDef action, StartEffectSignal effect)
    {
        var (date, time) = LocalTimeHelper.ToLocal(effect.Horizon, world.UtcOffset);
        var localTime = string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd} {time:HH:mm}");
        var placeName = world.Places.FirstOrDefault(place => place.Id == effect.Place.Value)?.Name ?? effect.Place.Value;
        var examples = string.Join('\n', personaDef.Examples.Select(example => $"- {example}"));
        var system = $"""
            あなたは、次の人物として、スマホに独り言を1つ書きます。
            人物：{personaDef.Name}
            {personaDef.Lore}
            発言例：
            {examples}
            世界：{world.Name}
            {world.Lore}
            書くのは本文だけです。説明、見出し、引用符、複数の案は書きません。
            """;
        var recent = string.Join('\n', persona.Posting.Recent.Select(post => $"- {post.Body}"));
        var user = $"""
            いまの時刻：{localTime}
            いまの行動：{action.Display}
            いる場所：{placeName}
            自分の直近の投稿（同じことは書かない）：
            {(recent.Length > 0 ? recent : "- まだない")}
            """;
        return new LlmRequest(LlmTier.Main, Purpose, system, [new LlmMessage(LlmRole.User, user)]);
    }
}
