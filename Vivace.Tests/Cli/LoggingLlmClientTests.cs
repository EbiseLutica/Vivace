using Vivace.Cli;
using Vivace.Core.Llm;

namespace Vivace.Tests.Cli;

/// <summary>LLM の失敗を1行書いて、例外を投げ直す包み。取り消しは書かない。</summary>
public class LoggingLlmClientTests
{
    /// <summary>失敗は、目的と例外の型名とメッセージの1行になり、例外はそのまま投げ直す。</summary>
    [Fact]
    public async Task FailureWritesOneLineAndRethrows()
    {
        var errors = new StringWriter { NewLine = "\n" };
        var client = new LoggingLlmClient(new FailingLlm(new InvalidOperationException("壊れた")), errors);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateTextAsync(Request(), CancellationToken.None));

        Assert.Equal("壊れた", error.Message);
        Assert.Equal("LLM の呼び出しに失敗した（目的 post.compose）: InvalidOperationException: 壊れた\n", errors.ToString());
    }

    /// <summary>メッセージに改行があっても、書かれるのは1行。改行は空白に変わる。</summary>
    [Fact]
    public async Task MultiLineMessageIsWrittenAsOneLine()
    {
        var errors = new StringWriter { NewLine = "\n" };
        var client = new LoggingLlmClient(new FailingLlm(new InvalidOperationException("Status Code: Unauthorized\n{\"type\":\"error\"}")), errors);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateTextAsync(Request(), CancellationToken.None));

        Assert.Equal("LLM の呼び出しに失敗した（目的 post.compose）: InvalidOperationException: Status Code: Unauthorized {\"type\":\"error\"}\n", errors.ToString());
    }

    /// <summary>取り消しでない TaskCanceledException（タイムアウトなど）は、失敗として1行書いてから投げ直す。</summary>
    [Fact]
    public async Task TaskCanceledWithoutCancellationIsWrittenAsFailure()
    {
        var errors = new StringWriter { NewLine = "\n" };
        var client = new LoggingLlmClient(new FailingLlm(new TaskCanceledException("timeout")), errors);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GenerateTextAsync(Request(), CancellationToken.None));

        Assert.Equal("LLM の呼び出しに失敗した（目的 post.compose）: TaskCanceledException: timeout\n", errors.ToString());
    }

    /// <summary>取り消しで止まったときは、何も書かずに例外を投げ直す。</summary>
    [Fact]
    public async Task CancellationWritesNothing()
    {
        var errors = new StringWriter { NewLine = "\n" };
        var client = new LoggingLlmClient(new WaitingLlm(), errors);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GenerateTextAsync(Request(), cancel.Token));

        Assert.Equal(string.Empty, errors.ToString());
    }

    /// <summary>テスト用の呼び出し。目的は post.compose。</summary>
    private static LlmRequest Request() => new(LlmTier.Main, "post.compose", "指示", [new LlmMessage(LlmRole.User, "本文")]);

    /// <summary>必ず指定の例外で失敗する LLM。</summary>
    /// <param name="error">投げる例外。</param>
    private sealed class FailingLlm(Exception error) : ILlmClient
    {
        /// <summary>例外で失敗する。</summary>
        /// <param name="request">呼び出しの内容（使わない）。</param>
        /// <param name="ct">キャンセル（使わない）。</param>
        public Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct) => Task.FromException<LlmResult<string>>(error);
    }

    /// <summary>取り消されるまで待つ LLM。</summary>
    private sealed class WaitingLlm : ILlmClient
    {
        /// <summary>取り消されるまで待つ（実際にはすぐ取り消される）。</summary>
        /// <param name="request">呼び出しの内容（使わない）。</param>
        /// <param name="ct">キャンセル。</param>
        public async Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new LlmResult<string>("本文", new LlmUsage("waiting", 0, 0));
        }
    }
}
