namespace Vivace.Core.Llm;

/// <summary>メッセージの発言者。</summary>
public enum LlmRole
{
    /// <summary>利用者側（ハーネスが渡す入力）。</summary>
    User,

    /// <summary>モデル側（過去の応答）。</summary>
    Assistant,
}
