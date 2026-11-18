using Vivace.Core.Domain.State;
using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>行動の開始。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">場所。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="Reason">理由（chosen / block / reflex / late）。</param>
/// <param name="ActivityVersion">行動のバージョン。</param>
/// <param name="BlockId">ブロックの行動のときのブロック ID。</param>
/// <param name="IsSubAction">副行動かどうか。副行動は主の行動を変えず、Overlay に入る。</param>
public sealed record ActionStartedPayload(
    string ActionId,
    PlaceId Place,
    TimeSpan Duration,
    ActivityReason Reason,
    long ActivityVersion,
    string? BlockId,
    bool IsSubAction = false) : EventPayload(1);
