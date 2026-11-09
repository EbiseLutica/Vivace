namespace Vivace.Core.Events;

/// <summary>投稿の空振り（段 7）。話題の選び方などの抽選に引いた範囲を持つ。</summary>
/// <param name="Reason">理由。</param>
/// <param name="Draws">抽選に引いた範囲（引いていなければ null）。</param>
public sealed record PostSkippedPayload(PostSkipReason Reason, RngDraws? Draws) : EventPayload(1);
