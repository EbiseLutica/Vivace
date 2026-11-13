namespace Vivace.Core.Domain.State;

/// <summary>移動のあとに始める行動。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動する場所。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="Reason">始まった理由。</param>
/// <param name="BlockId">ブロックの行動のときのブロック ID。</param>
public sealed record PendingAction(string ActionId, PlaceId Place, TimeSpan Duration, ActivityReason Reason, string? BlockId);
