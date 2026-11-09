namespace Vivace.Core.Events;

/// <summary>日の始まり（段 1）。窓口（ワールドの生成時）か、day.started の Apply が積む。</summary>
/// <param name="Date">現地の日付。</param>
public sealed record DayStartedPayload(DateOnly Date) : EventPayload(1);
