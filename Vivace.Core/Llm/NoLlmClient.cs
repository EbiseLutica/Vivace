namespace Vivace.Core.Llm;

/// <summary>LLM を指定しなかったときの実装。呼ぶと必ず失敗し、ハンドラは失敗として代わりの出力にする。</summary>
internal sealed class NoLlmClient : ILlmClient
{
    /// <summary>必ず失敗する。</summary>
    /// <param name="request">呼び出しの内容（使わない）。</param>
    /// <param name="ct">キャンセル（使わない）。</param>
    public Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct) =>
        Task.FromException<LlmResult<string>>(new InvalidOperationException("LLM がありません。UseLlm で指定してください。"));
}
