using Anthropic;
using Vivace.Core.Llm;

namespace Vivace.Llm.Anthropic;

/// <summary>
/// Anthropic の Messages API による ILlmClient。API キーは環境変数から読み、クライアントの再試行は行わない（再試行はコアの責務）。
/// </summary>
public sealed class AnthropicLlmClient : ILlmClient
{
    /// <summary>API キーを入れる環境変数の名前。</summary>
    public const string ApiKeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>SDK のクライアント。</summary>
    private readonly AnthropicClient _client;

    /// <summary>段とモデルの対応、応答の上限。</summary>
    private readonly AnthropicLlmOptions _options;

    /// <summary>SDK のクライアントと設定から作る。</summary>
    /// <param name="client">SDK のクライアント。</param>
    /// <param name="options">設定。</param>
    private AnthropicLlmClient(AnthropicClient client, AnthropicLlmOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <summary>
    /// API キーから作る。キーが空なら null を返す（LLM を使わずに動かすため）。キーは保持するだけで、表示しない。
    /// </summary>
    /// <param name="apiKey">API キー。</param>
    /// <param name="options">設定。</param>
    public static AnthropicLlmClient? FromApiKey(string? apiKey, AnthropicLlmOptions options) => Build(apiKey, options, null);

    /// <summary>
    /// API キーと、任意の HTTP クライアントから作る。テストで通信を差し替えるために使う。キーが空なら null。
    /// </summary>
    /// <param name="apiKey">API キー。</param>
    /// <param name="options">設定。</param>
    /// <param name="httpClient">SDK に使わせる HTTP クライアント（省略すると SDK の既定）。</param>
    internal static AnthropicLlmClient? Build(string? apiKey, AnthropicLlmOptions options, HttpClient? httpClient)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var client = httpClient is null
            ? new AnthropicClient { ApiKey = apiKey, MaxRetries = 0, Timeout = options.Timeout }
            : new AnthropicClient { ApiKey = apiKey, MaxRetries = 0, Timeout = options.Timeout, HttpClient = httpClient };
        return new AnthropicLlmClient(client, options);
    }

    /// <summary>環境変数 ANTHROPIC_API_KEY から作る。未設定なら null を返す。</summary>
    /// <param name="options">設定。</param>
    public static AnthropicLlmClient? FromEnvironment(AnthropicLlmOptions options) =>
        FromApiKey(Environment.GetEnvironmentVariable(ApiKeyVariable), options);

    /// <summary>1回の呼び出しをして、本文と使用量を返す。拒否、最大トークン数の切れ、空の本文は例外にする。</summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="ct">キャンセル。SDK の呼び出しに渡す。</param>
    public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
    {
        var parameters = AnthropicRequestMapper.ToParams(request, _options);
        var message = await _client.Messages.Create(parameters, ct);
        var texts = message.Content
            .Select(block => block.TryPickText(out var text) ? text.Text : string.Empty)
            .Where(text => text.Length > 0);
        return AnthropicResponseMapper.ToResult(message.StopReason?.Raw(), texts, message.Model.Raw(), message.Usage.InputTokens, message.Usage.OutputTokens);
    }
}
