using Vivace.Llm.Anthropic;

namespace Vivace.Tests.Llm;

/// <summary>応答から LlmResult への変換（本文の連結、使用量、拒否と切れと空の例外）。</summary>
public class AnthropicResponseMapperTests
{
    /// <summary>text ブロックは順に連結して本文になり、使用量とモデル名を持つ。</summary>
    [Fact]
    public void ConcatenatesTextBlocksAndKeepsUsage()
    {
        var result = AnthropicResponseMapper.ToResult("end_turn", ["今日の", "授業眠い"], "claude-haiku-5-5", 120, 7);

        Assert.Equal("今日の授業眠い", result.Value);
        Assert.Equal("claude-haiku-5-5", result.Usage.Model);
        Assert.Equal(120, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
    }

    /// <summary>停止理由がなくても、本文があれば返す。</summary>
    [Fact]
    public void NullStopReasonWithTextReturnsText()
    {
        var result = AnthropicResponseMapper.ToResult(null, ["本文"], "m", 1, 1);

        Assert.Equal("本文", result.Value);
    }

    /// <summary>拒否は例外にする。本文があっても返さない。</summary>
    [Fact]
    public void RefusalThrows()
    {
        Assert.Throws<InvalidOperationException>(() => AnthropicResponseMapper.ToResult("refusal", ["本文"], "m", 1, 1));
    }

    /// <summary>最大トークン数で切れたら例外にする。途中の本文は返さない。</summary>
    [Fact]
    public void MaxTokensThrows()
    {
        Assert.Throws<InvalidOperationException>(() => AnthropicResponseMapper.ToResult("max_tokens", ["途中"], "m", 1, 4096));
    }

    /// <summary>本文が空（text ブロックがない、または空文字だけ）なら例外にする。</summary>
    [Fact]
    public void EmptyBodyThrows()
    {
        Assert.Throws<InvalidOperationException>(() => AnthropicResponseMapper.ToResult("end_turn", [], "m", 1, 0));
        Assert.Throws<InvalidOperationException>(() => AnthropicResponseMapper.ToResult("end_turn", [""], "m", 1, 0));
    }

    /// <summary>成功の停止理由（stop_sequence）は本文を返す。</summary>
    [Fact]
    public void StopSequenceReturnsText()
    {
        var result = AnthropicResponseMapper.ToResult("stop_sequence", ["本文"], "m", 1, 1);

        Assert.Equal("本文", result.Value);
    }

    /// <summary>許可リストにない停止理由は、本文があっても例外にする。</summary>
    /// <param name="stopReason">停止理由。</param>
    [Theory]
    [InlineData("model_context_window_exceeded")]
    [InlineData("tool_use")]
    [InlineData("pause_turn")]
    public void UnknownStopReasonThrows(string stopReason)
    {
        Assert.Throws<InvalidOperationException>(() => AnthropicResponseMapper.ToResult(stopReason, ["本文"], "m", 1, 1));
    }
}
