using Vivace.Core.Llm;

namespace Vivace.Cli;

/// <summary>
/// LLM の呼び出しの失敗を1行、エラーの出力に書いてから、例外を投げ直す。取り消しは失敗として書かない。
/// </summary>
internal sealed class LoggingLlmClient(ILlmClient inner, TextWriter errors) : ILlmClient
{
    /// <summary>中の LLM を呼ぶ。失敗したら、目的と例外の型名とメッセージを1行書く。</summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="ct">キャンセル。</param>
    public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
    {
        try
        {
            return await inner.GenerateTextAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.WriteLine($"LLM の呼び出しに失敗した（目的 {request.Purpose}）: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
