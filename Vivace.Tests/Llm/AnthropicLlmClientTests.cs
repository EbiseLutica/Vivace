using Vivace.Llm.Anthropic;

namespace Vivace.Tests.Llm;

/// <summary>API キーからの生成。キーがなければ作らない（通信はしない）。</summary>
public class AnthropicLlmClientTests
{
    /// <summary>キーがなければ null（空と空白も含む）。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoKeyReturnsNull(string? apiKey)
    {
        Assert.Null(AnthropicLlmClient.FromApiKey(apiKey, new AnthropicLlmOptions()));
    }

    /// <summary>キーがあれば作る。キーは保持するだけで、通信はしない。</summary>
    [Fact]
    public void KeyCreatesTheClient()
    {
        Assert.NotNull(AnthropicLlmClient.FromApiKey("test-key-not-used", new AnthropicLlmOptions()));
    }
}
