namespace Vivace.Core.Llm;

/// <summary>LLM の呼び出し。本文を返す。</summary>
public interface ILlmClient
{
    /// <summary>1回の呼び出しをして、本文を返す。失敗は例外で伝える。</summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="ct">キャンセル。</param>
    Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct);
}
