using System.Collections.Immutable;
using Vivace.Core.Events;

namespace Vivace.Core.Domain;

/// <summary>ワールドインスタンスの状態。</summary>
public enum WorldStatus
{
    /// <summary>動いている。</summary>
    Alive,

    /// <summary>破棄された（記録として残る）。</summary>
    Destroyed,
}

/// <summary>ペルソナインスタンスの状態。</summary>
public enum PersonaStatus
{
    /// <summary>動いている。</summary>
    Alive,

    /// <summary>追いつきの上限を超えた空白の区間に入っている。</summary>
    Gap,

    /// <summary>破棄された（記録として残る）。</summary>
    Destroyed,
}

/// <summary>生えた場所の状態。</summary>
public enum PlaceStatus
{
    /// <summary>候補。まだ採用されていない。</summary>
    Candidate,

    /// <summary>採用済み。経路と候補に入る。</summary>
    Adopted,

    /// <summary>廃止した。</summary>
    Retired,
}

/// <summary>変換の辞書の判定。</summary>
public enum TermVerdict
{
    /// <summary>そのまま通す。</summary>
    Pass,

    /// <summary>別の語に変換する。</summary>
    Convert,

    /// <summary>却下する。</summary>
    Reject,
}

/// <summary>状態変数の種類。</summary>
public enum VitalName
{
    /// <summary>気力。</summary>
    Energy,

    /// <summary>健康度。</summary>
    Health,

    /// <summary>退屈度。</summary>
    Boredom,

    /// <summary>社交欲求。</summary>
    Social,
}

/// <summary>行動が始まった理由。</summary>
public enum ActivityReason
{
    /// <summary>Decider が選んだ。</summary>
    Chosen,

    /// <summary>固定・計画のブロックの行動。</summary>
    Block,

    /// <summary>反射（しきい値を越えた）。</summary>
    Reflex,

    /// <summary>予定の乱れへの対応で、遅刻して始めた。</summary>
    Late,
}

/// <summary>プランの由来。</summary>
public enum PlanSource
{
    /// <summary>LLM の Planner が作った。</summary>
    Llm,

    /// <summary>固定予定だけのプラン（代わりの出力）。</summary>
    Fallback,
}

/// <summary>プランのブロックの種類。</summary>
public enum BlockKind
{
    /// <summary>固定予定のブロック。</summary>
    Fixed,

    /// <summary>Planner が埋めた計画のブロック。</summary>
    Planned,

    /// <summary>自由時間。Decider が決める。</summary>
    Free,
}

/// <summary>
/// 世界の状態。Apply のたびに変わったところだけを作り直す（D3 3.7）。
/// </summary>
/// <param name="Id">ワールドインスタンスの ID。</param>
/// <param name="DefId">使っている定義の ID。</param>
/// <param name="Seed">乱数のワールドの種。</param>
/// <param name="Status">状態。</param>
/// <param name="GeneratedPlaces">生えた場所（候補、採用、廃止）。</param>
/// <param name="Terms">変換の辞書の、実行中の記録。</param>
/// <param name="Incidents">発生中の出来事。キーは発生のイベントの連番。</param>
/// <param name="GraphVersion">場所のグラフが変わるたびに上がる版。</param>
public sealed record WorldState(
    WorldId Id,
    string DefId,
    ulong Seed,
    WorldStatus Status,
    ImmutableDictionary<PlaceId, RuntimePlace> GeneratedPlaces,
    ImmutableDictionary<string, TermEntry> Terms,
    ImmutableDictionary<long, ActiveIncident> Incidents,
    long GraphVersion);

/// <summary>生えた場所。</summary>
/// <param name="Id">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">場所のタグ。</param>
/// <param name="Group">所属のまとまり（家など）。</param>
/// <param name="Links">つながる場所と移動時間（分）。ID の順に並べる。</param>
/// <param name="Status">状態。</param>
/// <param name="CreatedSeq">生えたときのイベントの連番。</param>
/// <param name="UpdatedSeq">状態が最後に変わったときのイベントの連番。</param>
public sealed record RuntimePlace(
    PlaceId Id,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<PlaceLink> Links,
    PlaceStatus Status,
    long CreatedSeq,
    long UpdatedSeq);

/// <summary>変換の辞書の1件。</summary>
/// <param name="Term">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="Mapped">変換先（変換のとき）。</param>
/// <param name="Category">構造に載るかの分類。</param>
/// <param name="CreatedSeq">記録したイベントの連番。</param>
public sealed record TermEntry(string Term, TermVerdict Verdict, string? Mapped, string? Category, long CreatedSeq);

/// <summary>発生中の出来事。解決済みの効果を持つので、解消のときに定義を引き直さなくてよい。</summary>
/// <param name="Seq">発生のイベントの連番（ID を兼ねる）。</param>
/// <param name="DefId">出来事の定義 ID。その場の定義なら null。</param>
/// <param name="Persona">対象のペルソナ（ペルソナ向けのとき）。</param>
/// <param name="StartedAt">発生した時刻。</param>
/// <param name="EndsAt">解消の時刻。持続時間がなければ null。</param>
/// <param name="Effects">解決済みの効果（対象の場所を含む）。</param>
public sealed record ActiveIncident(
    long Seq,
    string? DefId,
    PersonaId? Persona,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndsAt,
    ResolvedIncidentEffects Effects);

/// <summary>ペルソナの居場所。場所にいるか、移動中かのどちらか。</summary>
public abstract record Location;

/// <summary>場所にいる。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record AtPlace(PlaceId Place) : Location;

/// <summary>移動中。どの場所にもいないものとして扱う（D5 5.10）。</summary>
/// <param name="From">出発地。</param>
/// <param name="To">目的地。</param>
/// <param name="Route">経路（辺の列）。出発時に確定し、途中で辺が伸びたら残りを積み直す（D5 5.10）。</param>
/// <param name="DepartedAt">出発の時刻。</param>
/// <param name="ArrivesAt">到着の時刻。</param>
/// <param name="Next">到着のあとに始める行動（J-134）。なければ null。</param>
public sealed record Traveling(
    PlaceId From,
    PlaceId To,
    ImmutableArray<RouteStep> Route,
    DateTimeOffset DepartedAt,
    DateTimeOffset ArrivesAt,
    PendingAction? Next = null) : Location;

/// <summary>移動のあとに始める行動（J-134）。到着の Apply が action.started を積むときの中身。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動する場所。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="Reason">始まった理由。</param>
/// <param name="BlockId">ブロックの行動のときのブロック ID。</param>
public sealed record PendingAction(string ActionId, PlaceId Place, TimeSpan Duration, ActivityReason Reason, string? BlockId);

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

/// <summary>
/// プランのブロック。開始と終了は UTC の絶対時刻。固定予定と計画のブロックは ID を持ち、自由時間は持たない（ID が null）。
/// 電話の指定、間隔、同行は、プランを作ったときの固定予定から写す（D1 1.12：変更は次のプランから）。
/// </summary>
/// <param name="BlockId">ブロックの ID（定義 ID@開始日）。自由時間だけ null。</param>
/// <param name="Kind">ブロックの種類。</param>
/// <param name="Start">開始。</param>
/// <param name="End">終了。</param>
/// <param name="ActionId">行動の定義 ID。自由時間は null。</param>
/// <param name="Place">場所。指定がなければ null。</param>
/// <param name="Phone">スマホを開けるか。</param>
/// <param name="PhoneEvery">長いブロックの途中の機会の間隔。</param>
/// <param name="With">同行するペルソナの定義 ID。</param>
/// <param name="OnPartnerAbsent">同行者が抜けたときの扱い。</param>
public sealed record PlanBlock(
    string? BlockId,
    BlockKind Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? ActionId,
    PlaceId? Place,
    PhonePolicy Phone,
    TimeSpan? PhoneEvery,
    ImmutableArray<string> With,
    PartnerAbsentPolicy OnPartnerAbsent);

/// <summary>1日のプラン（ホライズンからの範囲）。</summary>
/// <param name="Version">プランの版。古いものを捨てる判定に使う。</param>
/// <param name="HorizonStart">範囲の開始。</param>
/// <param name="HorizonEnd">範囲の終了。</param>
/// <param name="NextRebuildAt">次に作り直す時刻。</param>
/// <param name="Source">由来。</param>
/// <param name="Blocks">ブロックの列。</param>
/// <param name="Summary">期間の一言の要約（文脈に使う）。</param>
public sealed record DayPlan(
    long Version,
    DateTimeOffset HorizonStart,
    DateTimeOffset HorizonEnd,
    DateTimeOffset NextRebuildAt,
    PlanSource Source,
    ImmutableArray<PlanBlock> Blocks,
    string? Summary);

/// <summary>状態変数の1本。基準時刻での値と、いまの率から、値 + 率 × 経過時間で読む（D5 5.6）。</summary>
/// <param name="BaseTime">基準の時刻。</param>
/// <param name="BaseValue">基準の値。</param>
/// <param name="Rate">いまの率（1時間あたり）。</param>
public readonly record struct VitalValue(DateTimeOffset BaseTime, double BaseValue, double Rate);

/// <summary>4つの状態変数と、その版。</summary>
/// <param name="Energy">気力。</param>
/// <param name="Health">健康度。</param>
/// <param name="Boredom">退屈度。</param>
/// <param name="Social">社交欲求。</param>
/// <param name="Version">状態変数の版。率が変わるたびに上がる。</param>
public sealed record Vitals(
    VitalValue Energy,
    VitalValue Health,
    VitalValue Boredom,
    VitalValue Social,
    long Version);

/// <summary>ペルソナに付いている状態（風邪など）。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
/// <param name="Seq">付けたイベントの連番。</param>
/// <param name="EndsAt">外れる時刻。持続時間がなければ null。</param>
public sealed record AttachedCondition(string ConditionId, long Seq, DateTimeOffset? EndsAt);

/// <summary>行動ごとの利用の記録（制限の判定に使う）。</summary>
/// <param name="LastStartedAt">前回の開始の時刻。</param>
/// <param name="CountDate">今日の回数を数えている現地の日付。</param>
/// <param name="CountToday">今日の回数。</param>
/// <param name="Consecutive">直前から連続して同じ行動を続けた回数（繰り返しの減衰に使う）。</param>
public sealed record ActionUsage(DateTimeOffset? LastStartedAt, DateOnly CountDate, int CountToday, int Consecutive);

/// <summary>投稿の状態。</summary>
/// <param name="LastPostAt">最後に投稿した時刻。</param>
/// <param name="LastSkippedAt">最後に空振りした時刻。</param>
/// <param name="RemoteNotBefore">サーバーの制限で、次に投稿できる時刻。</param>
public sealed record PostingState(DateTimeOffset? LastPostAt, DateTimeOffset? LastSkippedAt, DateTimeOffset? RemoteNotBefore);

/// <summary>未送信の外向きの操作。</summary>
/// <param name="Seq">操作のイベントの連番（ID を兼ねる）。</param>
/// <param name="Channel">チャンネルの種類（"misskey" など）。</param>
/// <param name="OperationKind">操作の種類。コアの語彙は "react" と "follow"、ほかは "other"。</param>
/// <param name="IsSpeech">言ったこと（発話）かどうか。</param>
/// <param name="Payload">チャンネルだけが読む中身。</param>
public sealed record OutboundOp(
    long Seq,
    string Channel,
    string OperationKind,
    bool IsSpeech,
    System.Text.Json.JsonElement Payload);

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

/// <summary>適用の位置。最後に適用したイベントの時刻、段、連番と、適用番号。</summary>
/// <param name="Time">時刻。</param>
/// <param name="Stage">段。</param>
/// <param name="Seq">連番。</param>
/// <param name="ApplyNo">適用番号。</param>
public readonly record struct Position(DateTimeOffset Time, EventStage Stage, long Seq, long ApplyNo);

/// <summary>シミュレーションの全体の状態（不変）。</summary>
/// <param name="Position">最後に適用した位置。</param>
/// <param name="Worlds">ワールドの状態。</param>
/// <param name="Personas">ペルソナの状態。</param>
public sealed record SimState(
    Position Position,
    ImmutableDictionary<WorldId, WorldState> Worlds,
    ImmutableDictionary<PersonaId, PersonaState> Personas);
