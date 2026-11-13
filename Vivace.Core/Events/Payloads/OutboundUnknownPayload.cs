namespace Vivace.Core.Events.Payloads;

/// <summary>外向きの操作の送信結果が不明。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="Reason">理由。</param>
public sealed record OutboundUnknownPayload(long OperationSeq, string Reason) : EventPayload(1);
