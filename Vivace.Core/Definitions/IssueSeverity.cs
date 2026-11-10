namespace Vivace.Core.Definitions;

/// <summary>問題の重さ。エラーがあれば読み込まない。</summary>
public enum IssueSeverity
{
    /// <summary>読み込まない。</summary>
    Error,

    /// <summary>読み込むが、知らせる。</summary>
    Warning,
}
