using Vivace.Core.Llm;
using Vivace.Testing;

namespace Vivace.Tests.Llm;

/// <summary>台本の LLM。目的と入力の文字列で引き、当たらなければ例外にする。</summary>
public class ScriptedLlmTests
{
    /// <summary>目的と含む文字列が当たれば、台本の本文を返す。</summary>
    [Fact]
    public async Task ReturnsTheScriptedTextWhenPurposeAndTextMatch()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "図書室");

        var result = await llm.GenerateTextAsync(Request("post.compose", "図書室の話"), CancellationToken.None);

        Assert.Equal("今日の授業眠い", result.Value);
    }

    /// <summary>目的が違えば当たらず、例外にする（黙って空を返さない）。</summary>
    [Fact]
    public async Task ThrowsWhenNoScriptMatches()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "今日の授業眠い");

        await Assert.ThrowsAsync<InvalidOperationException>(() => llm.GenerateTextAsync(Request("inbox.reply", "本文"), CancellationToken.None));
    }

    /// <summary>含む文字列が入力になければ、当たらない。</summary>
    [Fact]
    public async Task ContainsMustAppearInTheInput()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "図書室");

        await Assert.ThrowsAsync<InvalidOperationException>(() => llm.GenerateTextAsync(Request("post.compose", "公園の話"), CancellationToken.None));
    }

    /// <summary>失敗の台本は、その例外で失敗する。</summary>
    [Fact]
    public async Task FailureScriptThrowsItsError()
    {
        var failure = new TimeoutException("わざと遅れた");
        var llm = new ScriptedLlm().Fail("post.compose", failure);

        var error = await Assert.ThrowsAsync<TimeoutException>(() => llm.GenerateTextAsync(Request("post.compose", "本文"), CancellationToken.None));
        Assert.Same(failure, error);
    }

    /// <summary>テスト用の呼び出しを作る。システムの指示は空、メッセージは1件。</summary>
    /// <param name="purpose">目的。</param>
    /// <param name="content">メッセージの本文。</param>
    private static LlmRequest Request(string purpose, string content) =>
        new(LlmTier.Main, purpose, "", [new LlmMessage(LlmRole.User, content)]);
}
