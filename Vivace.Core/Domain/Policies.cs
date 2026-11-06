namespace Vivace.Core.Domain;

/// <summary>スマホを開けるかの指定。</summary>
public enum PhonePolicy
{
    /// <summary>開けない。</summary>
    Closed,

    /// <summary>開ける。</summary>
    Open,
}

/// <summary>同行者が抜けたときの扱い（P-40）。</summary>
public enum PartnerAbsentPolicy
{
    /// <summary>そのまま続ける（既定）。</summary>
    Continue,

    /// <summary>こちらのブロックを終わらせる。</summary>
    Cancel,
}
