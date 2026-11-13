namespace Vivace.Core.Domain.State;

/// <summary>行動が始まった理由。</summary>
public enum ActivityReason
{
    /// <summary>Decider が選んだ。</summary>
    Chosen,

    /// <summary>固定・計画のブロックの行動。</summary>
    Block,

    /// <summary>反射（しきい値を越えた）。</summary>
    Reflex,

    /// <summary>予定の乱れへの対応で、遅刻して始めた。</summary>
    Late,
}
