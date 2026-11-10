namespace Vivace.Core.Events;

/// <summary>決めどきの理由。</summary>
public enum DecisionReason
{
    /// <summary>自由時間のブロックが始まった。</summary>
    Free,

    /// <summary>自由時間の中で、行動が終わった。</summary>
    Ended,

    /// <summary>出来事で行動が中断された。</summary>
    Interrupted,

    /// <summary>ブロックの開始時の確認が通らない（予定の乱れ）。</summary>
    Disruption,

    /// <summary>スマホを開く機会。</summary>
    Phone,

    /// <summary>出力が guard で却下された。</summary>
    Retry,
}
