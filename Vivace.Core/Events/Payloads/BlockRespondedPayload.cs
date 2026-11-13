namespace Vivace.Core.Events.Payloads;

/// <summary>予定の乱れへの対応の決定。</summary>
/// <param name="BlockId">ブロックの ID。</param>
/// <param name="Choice">選んだもの。</param>
/// <param name="Draws">乱数を引いた範囲（遅刻か欠席か待つかを選んだとき）。</param>
public sealed record BlockRespondedPayload(string BlockId, BlockRespondedPayload.DisruptionChoice Choice, RngDraws? Draws) : EventPayload(1)
{
    /// <summary>予定の乱れへの対応の選択。</summary>
    public enum DisruptionChoice
    {
        /// <summary>遅刻する。</summary>
        Late,

        /// <summary>欠席する。</summary>
        Absent,

        /// <summary>待つ。</summary>
        Wait,
    }
}
