using System.Collections.Immutable;

namespace Vivace.Core.Events;

/// <summary>受信箱の捌きの結果（段 7）。</summary>
/// <param name="Outcomes">捌いた通知と、それぞれの結果。</param>
/// <param name="Draws">受信箱の順番などの抽選に引いた範囲。</param>
public sealed record InboxHandledPayload(ImmutableArray<InboxOutcome> Outcomes, RngDraws? Draws) : EventPayload(1);
