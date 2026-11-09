namespace Vivace.Core.Events;

/// <summary>外向きの操作の送信完了。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="ExternalId">送信先での ID（あれば）。</param>
public sealed record OutboundSentPayload(long OperationSeq, string? ExternalId) : EventPayload(1);
