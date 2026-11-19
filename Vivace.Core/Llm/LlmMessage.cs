namespace Vivace.Core.Llm;

/// <summary>会話の1つのメッセージ。</summary>
/// <param name="Role">発言者。</param>
/// <param name="Content">本文。</param>
public sealed record LlmMessage(LlmRole Role, string Content);
