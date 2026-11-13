namespace Vivace.Core.Events.Payloads;

/// <summary>投稿の空振り。話題の選び方などの抽選に引いた範囲を持つ。</summary>
/// <param name="Reason">理由。</param>
/// <param name="Draws">抽選に引いた範囲（引いていなければ null）。</param>
public sealed record PostSkippedPayload(PostSkippedPayload.PostSkipReason Reason, RngDraws? Draws) : EventPayload(1)
{
    /// <summary>投稿が空振りした理由。</summary>
    public enum PostSkipReason
    {
        /// <summary>話題が全部却下された。</summary>
        AllRejected,

        /// <summary>文面の生成に失敗した。</summary>
        GenerationFailed,

        /// <summary>締め切りに間に合わなかった。</summary>
        Deadline,
    }
}
