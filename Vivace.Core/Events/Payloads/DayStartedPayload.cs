namespace Vivace.Core.Events.Payloads;

/// <summary>日の始まり。</summary>
/// <param name="Date">現地の日付。</param>
public sealed record DayStartedPayload(DateOnly Date) : EventPayload(1);
