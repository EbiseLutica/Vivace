namespace Vivace.Core.Domain;

/// <summary>同行者が抜けたときの扱い（P-40）。</summary>
public enum PartnerAbsentPolicy
{
    /// <summary>そのまま続ける（既定）。</summary>
    Continue,

    /// <summary>こちらのブロックを終わらせる。</summary>
    Cancel,
}
