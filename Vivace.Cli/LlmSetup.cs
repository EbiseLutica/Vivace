using Vivace.Core.Llm;
using Vivace.Llm.Anthropic;

namespace Vivace.Cli;

/// <summary>vivace run の LLM の準備。キーがあれば Anthropic を作り、なければ LLM なしで動く旨を1行書く。</summary>
internal static class LlmSetup
{
    /// <summary>Main の段のモデルを上書きする環境変数。</summary>
    public const string MainModelVariable = "VIVACE_LLM_MAIN_MODEL";

    /// <summary>Light の段のモデルを上書きする環境変数。</summary>
    public const string LightModelVariable = "VIVACE_LLM_LIGHT_MODEL";

    /// <summary>キーがないときに書く通知。</summary>
    public const string NoKeyNotice = "（LLM のキーがないので、投稿は文面を作れずにスキップする。ANTHROPIC_API_KEY を設定すると使う）";

    /// <summary>
    /// 環境変数から LLM を作る。キーがなければ通知を書いて null を返す。
    /// </summary>
    /// <param name="env">環境変数を読む関数。</param>
    /// <param name="output">通知の出力先。</param>
    public static ILlmClient? Create(Func<string, string?> env, TextWriter output)
    {
        var client = AnthropicLlmClient.FromApiKey(env(AnthropicLlmClient.ApiKeyVariable), Options(env));
        if (client is null)
        {
            output.WriteLine(NoKeyNotice);
        }

        return client;
    }

    /// <summary>既定の設定に、環境変数の段のモデルを上書きしたものを返す。空や空白の値は上書きしない。</summary>
    /// <param name="env">環境変数を読む関数。</param>
    internal static AnthropicLlmOptions Options(Func<string, string?> env)
    {
        var defaults = new AnthropicLlmOptions();
        return defaults with
        {
            MainModel = Override(env(MainModelVariable)) ?? defaults.MainModel,
            LightModel = Override(env(LightModelVariable)) ?? defaults.LightModel,
        };
    }

    /// <summary>環境変数の値を、前後の空白を除いて返す。空なら null。</summary>
    /// <param name="value">環境変数の値。</param>
    private static string? Override(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
