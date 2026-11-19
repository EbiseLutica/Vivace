using System.Net;
using System.Text;
using System.Text.Json;
using Vivace.Core.Llm;
using Vivace.Llm.Anthropic;

namespace Vivace.Tests.Llm;

/// <summary>SDK の呼び出し（偽の HTTP）。ボディ、本文、停止理由、再試行、取り消しを、通信なしで確かめる。</summary>
public class AnthropicHttpTests
{
    /// <summary>text ブロック2つ、end_turn、使用量、モデル名を持つ応答。</summary>
    private const string Ok = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-sonnet-5-5","content":[{"type":"text","text":"今日の"},{"type":"text","text":"授業眠い"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":120,"output_tokens":7}}
        """;

    /// <summary>成功の応答は本文を連結し、使用量とモデル名を返す。</summary>
    [Fact]
    public async Task SuccessReturnsConcatenatedTextAndUsage()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, Ok)));
        var client = Build(handler);

        var result = await client.GenerateTextAsync(Request(), CancellationToken.None);

        Assert.Equal("今日の授業眠い", result.Value);
        Assert.Equal("claude-sonnet-5-5", result.Usage.Model);
        Assert.Equal(120, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
    }

    /// <summary>拒否の停止理由は、本文があっても例外にする。</summary>
    [Fact]
    public async Task RefusalStopReasonThrows()
    {
        var refusal = Ok.Replace("\"end_turn\"", "\"refusal\"", StringComparison.Ordinal);
        var client = Build(new FakeHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, refusal))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateTextAsync(Request(), CancellationToken.None));
    }

    /// <summary>過負荷の応答は、再試行せずに1回だけ送って例外にする（再試行はコアの責務）。</summary>
    [Fact]
    public async Task OverloadedResponseIsNotRetriedByTheClient()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Json((HttpStatusCode)529, """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""")));
        var client = Build(handler);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GenerateTextAsync(Request(), CancellationToken.None));

        Assert.Equal(1, handler.Calls);
    }

    /// <summary>取り消しは SDK の呼び出しに届き、OperationCanceledException で止まる。</summary>
    [Fact]
    public async Task CancellationStopsTheCall()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return Json(HttpStatusCode.OK, Ok);
        });
        var client = Build(handler);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GenerateTextAsync(Request(), cancel.Token));
    }

    /// <summary>送るボディは、モデル、上限、system、user と assistant の順。thinking、temperature、top_p は含まない。</summary>
    [Fact]
    public async Task RequestBodyHasNoThinkingOrSamplingFields()
    {
        string? body = null;
        var handler = new FakeHandler(async (request, ct) =>
        {
            body = await request.Content!.ReadAsStringAsync(ct);
            return Json(HttpStatusCode.OK, Ok);
        });
        var client = Build(handler);
        var request = new LlmRequest(LlmTier.Light, "post.compose", "指示", [new LlmMessage(LlmRole.User, "挨拶"), new LlmMessage(LlmRole.Assistant, "やあ"), new LlmMessage(LlmRole.User, "続き")]);

        await client.GenerateTextAsync(request, CancellationToken.None);

        using var json = JsonDocument.Parse(body!);
        var root = json.RootElement;
        Assert.Equal("claude-haiku-5-5", root.GetProperty("model").GetString());
        Assert.Equal(4096, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("指示", root.GetProperty("system").GetString());
        var roles = root.GetProperty("messages").EnumerateArray().Select(message => message.GetProperty("role").GetString()).ToList();
        Assert.Equal(["user", "assistant", "user"], roles);
        Assert.False(root.TryGetProperty("thinking", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.False(root.TryGetProperty("top_p", out _));
    }

    /// <summary>テスト用のクライアント。通信は偽の HTTP にする。</summary>
    /// <param name="handler">偽の HTTP。</param>
    private static ILlmClient Build(FakeHandler handler) =>
        AnthropicLlmClient.Build("test-key-not-used", new AnthropicLlmOptions(), new HttpClient(handler))!;

    /// <summary>テスト用の呼び出し。末尾は利用者の発言。</summary>
    private static LlmRequest Request() => new(LlmTier.Main, "post.compose", "指示", [new LlmMessage(LlmRole.User, "挨拶")]);

    /// <summary>JSON の本文の応答を作る。</summary>
    /// <param name="status">状態。</param>
    /// <param name="json">本文の JSON。</param>
    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>送られた要求を数え、応答を関数で決める偽の HTTP ハンドラ。</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        /// <summary>送られた要求の数。</summary>
        public int Calls { get; private set; }

        /// <summary>要求を数えてから、応答を返す。</summary>
        /// <param name="request">要求。</param>
        /// <param name="ct">キャンセル。</param>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return respond(request, ct);
        }
    }
}
