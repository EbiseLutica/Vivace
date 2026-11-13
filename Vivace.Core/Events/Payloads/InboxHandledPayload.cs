using System.Collections.Immutable;

namespace Vivace.Core.Events.Payloads;

/// <summary>受信箱の捌きの結果。</summary>
/// <param name="Outcomes">捌いた通知と、それぞれの結果。</param>
/// <param name="Draws">受信箱の順番などの抽選に引いた範囲。</param>
public sealed record InboxHandledPayload(ImmutableArray<InboxHandledPayload.InboxOutcome> Outcomes, RngDraws? Draws) : EventPayload(1)
{
    /// <summary>受信箱の1件の結果。</summary>
    /// <param name="ItemSeq">捌いた通知の連番。</param>
    /// <param name="Result">結果。</param>
    public sealed record InboxOutcome(long ItemSeq, InboxResult Result);

    /// <summary>受信箱の1件の捌きの結果。</summary>
    public enum InboxResult
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
