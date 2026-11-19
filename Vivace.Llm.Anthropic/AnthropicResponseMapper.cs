using Vivace.Core.Llm;

namespace Vivace.Llm.Anthropic;

/// <summary>応答の本文と使用量を LlmResult にする。純粋な変換で、通信はしない。</summary>
internal static class AnthropicResponseMapper
{
    /// <summary>拒否の停止理由。</summary>
    public const string RefusalStopReason = "refusal";

    /// <summary>最大トークン数で切れた停止理由。</summary>
    public const string MaxTokensStopReason = "max_tokens";

    /// <summary>成功として扱う停止理由（null は停止理由がない応答）。</summary>
    private static readonly HashSet<string?> SuccessStopReasons = new(StringComparer.Ordinal) { "end_turn", "stop_sequence", null };

    /// <summary>
    /// 停止理由を確かめ、text のブロックを連結して本文にする。成功の停止理由以外、空の本文は例外にする。
    /// 拒否のときは本文を読まない。
    /// </summary>
    /// <param name="stopReason">停止理由（なければ null）。</param>
    /// <param name="texts">text ブロックの本文の列。</param>
    /// <param name="model">応答のモデル名。</param>
    /// <param name="inputTokens">入力のトークン数。</param>
    /// <param name="outputTokens">出力のトークン数。</param>
    public static LlmResult<string> ToResult(string? stopReason, IEnumerable<string> texts, string model, long inputTokens, long outputTokens)
    {
        if (stopReason == RefusalStopReason)
        {
            throw new InvalidOperationException("LLM が応答を拒否しました（stop_reason: refusal）。");
        }

        if (stopReason == MaxTokensStopReason)
        {
            throw new InvalidOperationException("LLM の応答が最大トークン数で切れました（stop_reason: max_tokens）。");
        }

        if (!SuccessStopReasons.Contains(stopReason))
        {
            throw new InvalidOperationException($"LLM の応答が想定外の停止理由で終わりました（stop_reason: {stopReason}）。");
        }

        var body = string.Concat(texts);
        if (body.Length == 0)
        {
            throw new InvalidOperationException("LLM の応答に本文がありません。");
        }

        return new LlmResult<string>(body, new LlmUsage(model, checked((int)inputTokens), checked((int)outputTokens)));
    }
}
