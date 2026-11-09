namespace Vivace.Core.Storage;

/// <summary>イベントの適用の結果の記録（D3 3.8 の event 表の1行に当たる）。</summary>
public enum EventStatus
{
    /// <summary>適用した。</summary>
    Applied,

    /// <summary>版が古いなどで捨てた。</summary>
    Skipped,

    /// <summary>guard が通らず却下した。</summary>
    Rejected,
}
