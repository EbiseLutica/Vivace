namespace Vivace.Core.Domain;

/// <summary>変換の辞書の判定。</summary>
public enum TermVerdict
{
    /// <summary>そのまま通す。</summary>
    Pass,

    /// <summary>別の語に変換する。</summary>
    Convert,

    /// <summary>却下する。</summary>
    Reject,
}
