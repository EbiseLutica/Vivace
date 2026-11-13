namespace Vivace.Core.Events.Payloads;

/// <summary>スマホを開く機会。</summary>
/// <param name="BlockId">いまのブロックの ID。</param>
public sealed record PhoneOpportunityPayload(string BlockId) : EventPayload(1);
