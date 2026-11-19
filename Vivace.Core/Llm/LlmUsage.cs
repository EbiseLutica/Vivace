namespace Vivace.Core.Llm;

/// <summary>1回の呼び出しの使用量。</summary>
/// <param name="Model">使ったモデルの名前。</param>
/// <param name="InputTokens">入力のトークン数。</param>
/// <param name="OutputTokens">出力のトークン数。</param>
public sealed record LlmUsage(string Model, int InputTokens, int OutputTokens);
