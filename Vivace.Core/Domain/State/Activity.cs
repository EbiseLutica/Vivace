namespace Vivace.Core.Domain.State;

/// <summary>実行中の行動。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動している場所。</param>
/// <param name="StartedAt">開始の時刻。</param>
/// <param name="EndsAt">終了の時刻。</param>
/// <param name="Reason">始まった理由。</param>
/// <param name="BlockId">固定・計画のブロックの ID（ブロックの行動のとき）。</param>
public sealed record Activity(
    string ActionId,
    PlaceId Place,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    ActivityReason Reason,
    string? BlockId);
