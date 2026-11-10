namespace Vivace.Core.Events;

/// <summary>予定の乱れへの対応の決定。</summary>
/// <param name="BlockId">ブロックの ID。</param>
/// <param name="Choice">選んだもの。</param>
/// <param name="Draws">乱数を引いた範囲（遅刻か欠席か待つかを選んだとき）。</param>
public sealed record BlockRespondedPayload(string BlockId, DisruptionChoice Choice, RngDraws? Draws) : EventPayload(1);
