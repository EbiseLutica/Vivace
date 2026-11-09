namespace Vivace.Core.Events;

/// <summary>行動の終わり（段 2）。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="ActivityVersion">行動の版。</param>
public sealed record ActionEndedPayload(string ActionId, long ActivityVersion) : EventPayload(1);
