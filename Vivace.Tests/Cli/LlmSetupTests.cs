using Vivace.Cli;
using Vivace.Llm.Anthropic;

namespace Vivace.Tests.Cli;

/// <summary>vivace run の LLM の準備（キーの有無と、モデルの上書き）。環境変数は直接読まず、関数で渡す。</summary>
public class LlmSetupTests
{
    /// <summary>キーがなければ LLM を作らず、通知の1行を書く。</summary>
    [Fact]
    public void NoKeyWritesTheNoticeAndReturnsNull()
    {
        var output = new StringWriter();

        var llm = LlmSetup.Create(_ => null, output);

        Assert.Null(llm);
        Assert.Equal(LlmSetup.NoKeyNotice + Environment.NewLine, output.ToString());
    }

    /// <summary>空白だけのキーも、キーがないものとして扱う。</summary>
    [Fact]
    public void BlankKeyIsTreatedAsNoKey()
    {
        var output = new StringWriter();

        var llm = LlmSetup.Create(name => name == AnthropicLlmClient.ApiKeyVariable ? "  " : null, output);

        Assert.Null(llm);
        Assert.Contains(LlmSetup.NoKeyNotice, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>キーがあれば LLM を作り、通知は書かない。</summary>
    [Fact]
    public void KeyCreatesTheClientWithoutNotice()
    {
        var output = new StringWriter();

        var llm = LlmSetup.Create(name => name == AnthropicLlmClient.ApiKeyVariable ? "test-key-not-used" : null, output);

        Assert.NotNull(llm);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>モデルの環境変数がなければ、既定のモデルを使う。</summary>
    [Fact]
    public void ModelsDefaultWhenVariablesAreUnset()
    {
        var options = LlmSetup.Options(_ => null);

        Assert.Equal("claude-sonnet-5-5", options.MainModel);
        Assert.Equal("claude-haiku-5-5", options.LightModel);
    }

    /// <summary>モデルの環境変数があれば、その段のモデルを上書きする。前後の空白は除く。空白だけは上書きしない。</summary>
    [Fact]
    public void ModelVariablesOverrideTheirTier()
    {
        var options = LlmSetup.Options(name => name switch
        {
            LlmSetup.MainModelVariable => " main-custom ",
            LlmSetup.LightModelVariable => "  ",
            _ => null,
        });

        Assert.Equal("main-custom", options.MainModel);
        Assert.Equal("claude-haiku-5-5", options.LightModel);
    }
}
