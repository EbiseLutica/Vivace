namespace Vivace.Core.Events;

/// <summary>外向きの操作の送信失敗。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="Reason">失敗の理由。</param>
/// <param name="RetryAfter">次に投稿できる時刻（あれば）。</param>
public sealed record OutboundFailedPayload(long OperationSeq, string Reason, DateTimeOffset? RetryAfter) : EventPayload(1);
