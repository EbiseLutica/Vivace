using Anthropic.Models.Messages;
using Vivace.Core.Llm;

namespace Vivace.Llm.Anthropic;

/// <summary>LlmRequest を Messages API のパラメータにする。純粋な変換で、通信はしない。</summary>
internal static class AnthropicRequestMapper
{
    /// <summary>
    /// 段からモデルを選び、system、メッセージ、最大トークン数を入れたパラメータを作る。
    /// thinking、temperature、top_p は送らない（送るとモデルによって 400 になる）。
    /// </summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="options">モデルと応答の上限。</param>
    public static MessageCreateParams ToParams(LlmRequest request, AnthropicLlmOptions options)
    {
        var messages = request.Messages.Select<LlmMessage, MessageParam>(message => message.Role switch
        {
            LlmRole.User => new UserMessageParam { Content = message.Content },
            LlmRole.Assistant => new AssistantMessageParam { Content = message.Content },
            _ => throw new ArgumentOutOfRangeException(nameof(request), message.Role, null),
        }).ToList();

        return new MessageCreateParams
        {
            Model = ModelFor(request.Tier, options),
            MaxTokens = options.MaxTokens,
            System = request.System,
            Messages = messages,
        };
    }

    /// <summary>段に対応するモデルの ID。</summary>
    /// <param name="tier">モデルの段。</param>
    /// <param name="options">段とモデルの対応。</param>
    internal static string ModelFor(LlmTier tier, AnthropicLlmOptions options) => tier switch
    {
        LlmTier.Main => options.MainModel,
        LlmTier.Light => options.LightModel,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };
}
