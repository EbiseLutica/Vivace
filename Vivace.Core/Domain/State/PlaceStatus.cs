namespace Vivace.Core.Domain.State;

/// <summary>生えた場所の状態。</summary>
public enum PlaceStatus
{
    /// <summary>候補。まだ採用されていない。</summary>
    Candidate,

    /// <summary>採用済み。経路と候補に入る。</summary>
    Adopted,

    /// <summary>廃止した。</summary>
    Retired,
}
