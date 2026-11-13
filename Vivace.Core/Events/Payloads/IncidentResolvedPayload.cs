namespace Vivace.Core.Events.Payloads;

/// <summary>出来事の解消。</summary>
/// <param name="IncidentSeq">発生したイベントの連番。</param>
public sealed record IncidentResolvedPayload(long IncidentSeq) : EventPayload(1);
