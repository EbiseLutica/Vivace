namespace Vivace.Core.Events;

/// <summary>追いつきの上限で飛ばした空白の区間。</summary>
/// <param name="From">空白の始まり。</param>
/// <param name="To">空白の終わり。</param>
public sealed record PersonaGapPayload(DateTimeOffset From, DateTimeOffset To) : EventPayload(1);
