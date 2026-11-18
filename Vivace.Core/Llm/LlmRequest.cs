namespace Vivace.Core.Llm;

/// <summary>LLM の呼び出しの内容。</summary>
/// <param name="Tier">モデルの段。</param>
/// <param name="Purpose">目的（"post.compose" など）。台本の引きに使い、モデルには見せない。</param>
/// <param name="System">システムの指示。</param>
/// <param name="Messages">メッセージの列。</param>
public sealed record LlmRequest(
    LlmTier Tier,
    string Purpose,
    string System,
    IReadOnlyList<LlmMessage> Messages);
