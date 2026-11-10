namespace Vivace.Core.Storage;

/// <summary>イベントの適用の結果の記録。</summary>
public enum EventStatus
{
    /// <summary>適用した。</summary>
    Applied,

    /// <summary>版が古いなどで捨てた。</summary>
    Skipped,

    /// <summary>guard が通らず却下した。</summary>
    Rejected,
}
