using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>ペルソナの状態。Apply のたびに変わったところだけを作り直す（D3 3.7）。</summary>
/// <param name="Id">インスタンスの ID。</param>
/// <param name="DefId">使っている定義の ID。</param>
/// <param name="World">居住するワールド。</param>
/// <param name="Status">状態。</param>
/// <param name="Location">居場所。</param>
/// <param name="Activity">実行中の行動。</param>
/// <param name="ActivityVersion">行動の版。古い決定を捨てる判定に使う。</param>
/// <param name="Overlay">固定ブロック中にスマホを使っている間の副行動。</param>
/// <param name="Vitals">状態変数。</param>
/// <param name="Conditions">付いている状態。</param>
/// <param name="Plan">いまのプラン。</param>
/// <param name="PlanVersion">プランの版。</param>
/// <param name="Usage">行動ごとの利用の記録。キーは行動の定義 ID。</param>
/// <param name="Rng">用途ごとの乱数の位置。キーは用途。</param>
/// <param name="Posting">投稿の状態。</param>
/// <param name="Inbox">未処理の通知。</param>
/// <param name="Outbound">未送信の操作。</param>
/// <param name="LastWake">日誌の範囲の起点（最後に起きた時刻）。</param>
/// <param name="Deciding">決めどきの処理中かどうか（J-133）。決めどきの二重の積みを防ぐ。</param>
/// <param name="LastBlockId">最後に始めたブロックの ID（J-135）。同じブロックは一度だけ始める。</param>
/// <param name="PlannedFor">Planner を起こした期間の鍵（記憶の整理の行動のブロック ID、または時刻）。同じ期間では一度だけ動く（D5 5.5、J-141）。</param>
public sealed record PersonaState(
    PersonaId Id,
    string DefId,
    WorldId World,
    PersonaStatus Status,
    Location Location,
    Activity? Activity,
    long ActivityVersion,
    Activity? Overlay,
    Vitals Vitals,
    ImmutableArray<AttachedCondition> Conditions,
    DayPlan? Plan,
    long PlanVersion,
    ImmutableDictionary<string, ActionUsage> Usage,
    ImmutableDictionary<string, ulong> Rng,
    PostingState Posting,
    ImmutableList<InboxItem> Inbox,
    ImmutableList<OutboundOp> Outbound,
    DateTimeOffset? LastWake,
    bool Deciding = false,
    string? LastBlockId = null,
    string? PlannedFor = null);
