namespace Vivace.Core.Events;

/// <summary>
/// 適用に失敗したときの動き（D1 1.7）。
/// </summary>
public enum RejectAction
{
    /// <summary>同じ時刻（いま）に決めどきを積み直す。</summary>
    Redecide,

    /// <summary>文面の作り直しを積む（辞書の食い違い）。</summary>
    Recompose,

    /// <summary>何もしない（割り込みで不要になった場合）。</summary>
    Drop,
}
