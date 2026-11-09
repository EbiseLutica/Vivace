namespace Vivace.Core.Domain;

/// <summary>会話の公開範囲。</summary>
public enum Audience
{
    /// <summary>公開。</summary>
    Public,

    /// <summary>DM。</summary>
    Dm,

    /// <summary>記憶にしない。</summary>
    NoMemory,
}
