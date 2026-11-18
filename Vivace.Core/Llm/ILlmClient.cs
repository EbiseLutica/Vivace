namespace Vivace.Core.Llm;

/// <summary>LLM の呼び出し。最小版は本文を返すだけ（構造化出力、ツール、再試行は、必要になったときに足す）。</summary>
public interface ILlmClient
{
    /// <summary>1回の呼び出しをして、本文を返す。失敗は例外で伝える。</summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="ct">キャンセル。</param>
    Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct);
}
