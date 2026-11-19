namespace Vivace.Llm.Anthropic;

/// <summary>
/// Anthropic の呼び出しの設定。段からモデルへの対応、応答の上限、時間切れを持つ。モデル ID は、日付の接尾辞を付けずに書く。
/// </summary>
public sealed record AnthropicLlmOptions
{
    /// <summary>Main の段のモデル。</summary>
    public string MainModel { get; init; } = "claude-sonnet-5-5";

    /// <summary>Light の段のモデル。</summary>
    public string LightModel { get; init; } = "claude-haiku-5-5";

    /// <summary>応答の最大トークン数。</summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>1回の呼び出しの時間切れ。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}
