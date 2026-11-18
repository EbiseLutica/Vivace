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
/// 投稿の効果（sns.post）。ペルソナの声で独り言を1つ作る。成功なら発話、LLM の失敗、例外、空の本文ならスキップにする。
/// </summary>
internal static class SnsPostHandler
{
    /// <summary>効果の名前（行動の effects.handler）。</summary>
    public const string Name = "sns.post";

    /// <summary>LLM の目的。</summary>
    public const string Purpose = "post.compose";

    /// <summary>投稿のチャンネル。チャンネルの実装が入るまでは、コンソールの固定の名前。</summary>
    public const string Channel = "console";

    /// <summary>チャンネルの中身。コンソールは追加の情報を持たない。</summary>
    private static readonly JsonElement EmptyChannelPayload = JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// 独り言を1つ作り、出力のイベントを返す。出力の時刻は行動の終わり。行動のバージョンが変わっていたら、guard で捨てられる。
    /// </summary>
    /// <param name="ctx">規則のコンテキスト（定義とルート）。</param>
    /// <param name="llm">LLM。</param>
    /// <param name="snapshot">起動の時点の状態。</param>
    /// <param name="effect">起動のシグナル。</param>
    public static async Task<EventDraft> RunAsync(RuleContext ctx, ILlmClient llm, SimState snapshot, StartEffectSignal effect)
    {
        var persona = snapshot.Personas[effect.Persona];
        var guard = new Guard([new ActivityVersionCheck(effect.ActivityVersion)], RejectAction.Drop);
        try
        {
            var world = ctx.Defs.Worlds[snapshot.Worlds[persona.World].DefId];
            var personaDef = ctx.Defs.Personas[persona.DefId];
            var action = ctx.Defs.Actions[effect.ActionId];
            var result = await llm.GenerateTextAsync(Request(personaDef, world, persona, action, effect), CancellationToken.None);
            var body = result.Value.Trim();
            if (body.Length > 0)
            {
                var speech = new OutboundSpeechPayload(Channel, OutboundSpeechPayload.OutboundPostKind.Post, body, null, Audience.Public, [effect.BatchSeq], EmptyChannelPayload, null);
                return new EventDraft(effect.Horizon, EventTypes.OutboundSpeech, persona.World, persona.Id, speech, guard, effect.BatchSeq);
            }
        }
        catch (Exception)
        {
            // 失敗は、下でスキップとして残す（エンジンは止めない）。
        }

        return Skipped(snapshot, effect);
    }

    /// <summary>
    /// 投稿をスキップする出力（生成の失敗）。ハンドラが失敗したときの代わりの出力にも使う。
    /// </summary>
    /// <param name="snapshot">起動の時点の状態。</param>
    /// <param name="effect">起動のシグナル。</param>
    public static EventDraft Skipped(SimState snapshot, StartEffectSignal effect)
    {
        var persona = snapshot.Personas[effect.Persona];
        var guard = new Guard([new ActivityVersionCheck(effect.ActivityVersion)], RejectAction.Drop);
        var payload = new PostSkippedPayload(PostSkippedPayload.PostSkipReason.GenerationFailed, null);
        return new EventDraft(effect.Horizon, EventTypes.PostSkipped, persona.World, persona.Id, payload, guard, effect.BatchSeq);
    }

    /// <summary>
    /// 文面の呼び出し。システムの指示にはペルソナと世界の lore、メッセージには時刻、行動、場所、直近の投稿を置く。
    /// </summary>
    /// <param name="personaDef">ペルソナの定義。</param>
    /// <param name="world">ワールドの定義。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="action">行動の定義。</param>
    /// <param name="effect">起動のシグナル（時刻と場所）。</param>
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
