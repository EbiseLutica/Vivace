using Anthropic.Models.Messages;
using Vivace.Core.Llm;
using Vivace.Llm.Anthropic;

namespace Vivace.Tests.Llm;

/// <summary>LlmRequest から Messages API のパラメータへの変換。</summary>
public class AnthropicRequestMapperTests
{
    /// <summary>Main の段は Main のモデル、Light の段は Light のモデルになる。</summary>
    [Fact]
    public void TierSelectsTheConfiguredModel()
    {
        var options = new AnthropicLlmOptions { MainModel = "main-x", LightModel = "light-y" };

        var main = AnthropicRequestMapper.ToParams(Request(LlmTier.Main), options);
        var light = AnthropicRequestMapper.ToParams(Request(LlmTier.Light), options);

        Assert.Equal("main-x", main.Model.Raw());
        Assert.Equal("light-y", light.Model.Raw());
    }

    /// <summary>既定のモデルと上限は、オプションの既定値（claude-sonnet-5-5、claude-haiku-5-5、4096）。</summary>
    [Fact]
    public void DefaultsAreOpus55HaikuAnd4096Tokens()
    {
        var main = AnthropicRequestMapper.ToParams(Request(LlmTier.Main), new AnthropicLlmOptions());
        var light = AnthropicRequestMapper.ToParams(Request(LlmTier.Light), new AnthropicLlmOptions());

        Assert.Equal("claude-sonnet-5-5", main.Model.Raw());
        Assert.Equal("claude-haiku-5-5", light.Model.Raw());
        Assert.Equal(4096, main.MaxTokens);
    }

    /// <summary>システムの指示は、トップの system に入る。メッセージには入らない。</summary>
    [Fact]
    public void SystemGoesToTheTopLevelSystemField()
    {
        var parameters = AnthropicRequestMapper.ToParams(Request(LlmTier.Main), new AnthropicLlmOptions());

        Assert.True(parameters.System!.TryPickString(out var system));
        Assert.Equal("指示", system);
        Assert.Equal(3, parameters.Messages.Count);
        Assert.All(parameters.Messages, message => Assert.False(message.TryPickSystem(out _)));
    }

    /// <summary>User と Assistant は、それぞれの role のメッセージになり、順序を保つ。</summary>
    [Fact]
    public void RolesMapToUserAndAssistantInOrder()
    {
        var parameters = AnthropicRequestMapper.ToParams(Request(LlmTier.Main), new AnthropicLlmOptions());

        Assert.True(parameters.Messages[0].TryPickUser(out var user));
        Assert.Equal("こんにちは", user.Content.TryPickString(out var userText) ? userText : null);
        Assert.True(parameters.Messages[1].TryPickAssistant(out var assistant));
        Assert.Equal("やあ", assistant.Content.TryPickString(out var assistantText) ? assistantText : null);
    }

    /// <summary>末尾が assistant の呼び出し（prefill）は、送る前に拒否する。</summary>
    [Fact]
    public void TrailingAssistantMessageIsRejected()
    {
        var request = new LlmRequest(LlmTier.Main, "test", "指示", [new LlmMessage(LlmRole.User, "挨拶"), new LlmMessage(LlmRole.Assistant, "やあ")]);

        Assert.Throws<ArgumentException>(() => AnthropicRequestMapper.ToParams(request, new AnthropicLlmOptions()));
    }

    /// <summary>テスト用の呼び出し。system は「指示」、メッセージは利用者の挨拶と、モデルの応答。</summary>
    /// <param name="tier">モデルの段。</param>
    private static LlmRequest Request(LlmTier tier) => new(
        tier,
        "test",
        "指示",
        [new LlmMessage(LlmRole.User, "こんにちは"), new LlmMessage(LlmRole.Assistant, "やあ"), new LlmMessage(LlmRole.User, "続き")]);
}
