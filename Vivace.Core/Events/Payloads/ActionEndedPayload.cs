namespace Vivace.Core.Events.Payloads;

/// <summary>行動の終わり。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="ActivityVersion">行動のバージョン。</param>
/// <param name="IsSubAction">副行動の終わりかどうか。</param>
public sealed record ActionEndedPayload(string ActionId, long ActivityVersion, bool IsSubAction = false) : EventPayload(1);
