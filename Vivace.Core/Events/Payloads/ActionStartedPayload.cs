using Vivace.Core.Domain.State;
using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>行動の開始。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">場所。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="Reason">理由（chosen / block / reflex / late）。</param>
/// <param name="ActivityVersion">行動の版。</param>
/// <param name="BlockId">ブロックの行動のときのブロック ID。</param>
public sealed record ActionStartedPayload(
    string ActionId,
    PlaceId Place,
    TimeSpan Duration,
    ActivityReason Reason,
    long ActivityVersion,
    string? BlockId) : EventPayload(1);
