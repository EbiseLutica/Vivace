namespace Vivace.Core.Rules;

/// <summary>イベントの適用の結果。</summary>
public enum EventOutcome
{
    /// <summary>適用した。</summary>
    Applied,

    /// <summary>版が古い、または対象がないので捨てた。</summary>
    Skipped,

    /// <summary>guard が通らなかった。</summary>
    Rejected,
}
