using Vivace.Core.Llm;

namespace Vivace.Testing;

/// <summary>
/// 台本の LLM。目的（Purpose）と、入力（システムの指示とメッセージ）に含まれる文字列で応答を引く。
/// 当たる台本がなければ例外にする（黙って空を返さない）。台本は、呼ぶ前に組む。
/// </summary>
public sealed class ScriptedLlm : ILlmClient
{
    /// <summary>台本の列。先に足したものが優先する。</summary>
    private readonly List<Script> _scripts = [];

    /// <summary>目的と含む文字列に当たったら、本文を返す台本を足す。含む文字列を省くと、目的だけで当たる。</summary>
    /// <param name="purpose">目的。</param>
    /// <param name="text">返す本文。</param>
    /// <param name="contains">入力に含まれているべき文字列。</param>
    public ScriptedLlm Reply(string purpose, string text, string? contains = null)
    {
        _scripts.Add(new Script(purpose, contains, text, null));
        return this;
    }

    /// <summary>目的と含む文字列に当たったら、例外で失敗する台本を足す。</summary>
    /// <param name="purpose">目的。</param>
    /// <param name="error">投げる例外。</param>
    /// <param name="contains">入力に含まれているべき文字列。</param>
    public ScriptedLlm Fail(string purpose, Exception error, string? contains = null)
    {
        _scripts.Add(new Script(purpose, contains, null, error));
        return this;
    }

    /// <summary>台本から応答を引く。当たらなければ、テストを失敗させる例外にする。</summary>
    /// <param name="request">呼び出しの内容。</param>
    /// <param name="ct">キャンセル（使わない）。</param>
    public Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
    {
        var script = _scripts.FirstOrDefault(candidate => candidate.Matches(request));
        if (script is null)
        {
            return Task.FromException<LlmResult<string>>(new InvalidOperationException($"台本に当たる応答がありません（目的: {request.Purpose}）"));
        }

        if (script.Error is { } error)
        {
            return Task.FromException<LlmResult<string>>(error);
        }

        return Task.FromResult(new LlmResult<string>(script.Text!, new LlmUsage("scripted", 0, 0)));
    }

    /// <summary>台本1件。目的が一致し、含む文字列があれば入力のどこかに含まれること。</summary>
    /// <param name="Purpose">目的。</param>
    /// <param name="Contains">入力に含まれているべき文字列（なければ null）。</param>
    /// <param name="Text">返す本文（失敗の台本では null）。</param>
    /// <param name="Error">投げる例外（成功の台本では null）。</param>
    private sealed record Script(string Purpose, string? Contains, string? Text, Exception? Error)
    {
        /// <summary>この台本が、呼び出しに当たるか。</summary>
        /// <param name="request">呼び出しの内容。</param>
        public bool Matches(LlmRequest request) =>
            request.Purpose == Purpose
            && (Contains is null
                || request.System.Contains(Contains, StringComparison.Ordinal)
                || request.Messages.Any(message => message.Content.Contains(Contains, StringComparison.Ordinal)));
    }
}
