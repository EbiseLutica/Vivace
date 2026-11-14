using System.Collections.Immutable;

namespace Vivace.Core.Events.Payloads;

/// <summary>受信箱の処理の結果。</summary>
/// <param name="Outcomes">処理した通知と、それぞれの結果。</param>
/// <param name="Draws">受信箱の順番などの抽選に引いた範囲。</param>
public sealed record InboxHandledPayload(ImmutableArray<InboxHandledPayload.InboxOutcome> Outcomes, RngDraws? Draws) : EventPayload(1)
{
    /// <summary>受信箱の1件の結果。</summary>
    /// <param name="ItemSeq">処理した通知の連番。</param>
    /// <param name="Result">結果。</param>
    public sealed record InboxOutcome(long ItemSeq, InboxOutcomeKind Result);

    /// <summary>受信箱の1件の処理の結果。</summary>
    public enum InboxOutcomeKind
    {
        /// <summary>返信した。</summary>
        Reply,

        /// <summary>リアクションした。</summary>
        React,

        /// <summary>フォローし返した。</summary>
        FollowBack,

        /// <summary>見ただけ。</summary>
        Seen,

        /// <summary>何もしない。</summary>
        Nothing,
    }
}
