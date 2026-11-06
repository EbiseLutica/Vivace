using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>
/// イベントのペイロードの基底。版（"v"）を持つ。項目を足すときは版を上げず、省略可能な項目として足す（D3 3.9）。
/// </summary>
/// <param name="V">イベントタイプごとの版。</param>
public abstract record EventPayload(int V);

/// <summary>決めどきの理由（D2 2.3）。</summary>
public enum DecisionReason
{
    /// <summary>自由時間のブロックが始まった。</summary>
    Free,

    /// <summary>自由時間の中で、行動が終わった。</summary>
    Ended,

    /// <summary>出来事で行動が中断された。</summary>
    Interrupted,

    /// <summary>ブロックの開始時の確認が通らない（予定の乱れ）。</summary>
    Disruption,

    /// <summary>スマホを開く機会。</summary>
    Phone,

    /// <summary>出力が guard で却下された。</summary>
    Retry,
}

/// <summary>予定の乱れへの対応の選択（D5 5.4）。</summary>
public enum DisruptionChoice
{
    /// <summary>遅刻する。</summary>
    Late,

    /// <summary>欠席する。</summary>
    Absent,

    /// <summary>待つ。</summary>
    Wait,
}

/// <summary>状態変数の調整の仕方。</summary>
public enum VitalAdjustKind
{
    /// <summary>増減する。</summary>
    Add,

    /// <summary>値を設定する。</summary>
    Set,
}

/// <summary>外向きの投稿の種類。</summary>
public enum OutboundPostKind
{
    /// <summary>新しい投稿。</summary>
    Post,

    /// <summary>返信。</summary>
    Reply,
}

/// <summary>受信箱の1件の捌きの結果（D6 6.5）。</summary>
public enum InboxResult
{
    /// <summary>返信した。</summary>
    Reply,

    /// <summary>リアクションした。</summary>
    React,

    /// <summary>フォローし返した。</summary>
    FollowBack,

    /// <summary>見ただけ。</summary>
    Seen,

    /// <summary>何もしない。</summary>
    Nothing,
}

/// <summary>投稿が空振りした理由（D6 6.6）。</summary>
public enum PostSkipReason
{
    /// <summary>話題が全部却下された。</summary>
    AllRejected,

    /// <summary>文面の生成に失敗した。</summary>
    GenerationFailed,

    /// <summary>締め切りに間に合わなかった。</summary>
    Deadline,
}

/// <summary>乱数を引いた範囲。どの列の、どこからどこまで引いたかを残す（D5 5.9）。</summary>
/// <param name="Owner">持ち主。</param>
/// <param name="Purpose">用途。</param>
/// <param name="From">引き始めた位置。</param>
/// <param name="To">引き終わった後の位置。</param>
public sealed record RngDraws(string Owner, string Purpose, ulong From, ulong To);

/// <summary>受信箱の1件の結果。</summary>
/// <param name="ItemSeq">捌いた通知の連番。</param>
/// <param name="Result">結果。</param>
public sealed record InboxOutcome(long ItemSeq, InboxResult Result);

/// <summary>日の始まり（段 1）。窓口（ワールドの生成時）か、day.started の Apply が積む。</summary>
/// <param name="Date">現地の日付。</param>
public sealed record DayStartedPayload(DateOnly Date) : EventPayload(1);

/// <summary>行動の終わり（段 2）。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="ActivityVersion">行動の版。</param>
public sealed record ActionEndedPayload(string ActionId, long ActivityVersion) : EventPayload(1);

/// <summary>移動の到着（段 2）。</summary>
/// <param name="Place">着いた場所。</param>
/// <param name="ActivityVersion">行動の版。古ければ捨てる。</param>
public sealed record TravelArrivedPayload(PlaceId Place, long ActivityVersion) : EventPayload(1);

/// <summary>出来事の解消（段 2）。</summary>
/// <param name="IncidentSeq">発生したイベントの連番。</param>
public sealed record IncidentResolvedPayload(long IncidentSeq) : EventPayload(1);

/// <summary>出来事の発生（段 3）。定義を引かずに解消できるよう、解決済みの効果を持つ。抽選は日付つきの用途で毎日 0 番から引くので、位置は残さない（D5 5.9）。</summary>
/// <param name="DefId">出来事の定義 ID。その場の定義（語彙だけで書かれたもの）なら null。</param>
/// <param name="Persona">対象のペルソナ（ペルソナ向けのとき）。</param>
/// <param name="Duration">持続時間。なければ null。</param>
/// <param name="Effects">解決済みの効果（対象の場所を含む）。</param>
public sealed record IncidentOccurredPayload(
    string? DefId,
    PersonaId? Persona,
    TimeSpan? Duration,
    ResolvedIncidentEffects Effects) : EventPayload(1);

/// <summary>生えた場所の追加（段 3）。</summary>
/// <param name="Place">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">タグ。</param>
/// <param name="Group">所属のまとまり。</param>
/// <param name="Links">つながる場所と移動時間（分）。</param>
/// <param name="Status">状態（候補か採用済み）。</param>
/// <param name="Draws">抽選に引いた範囲（候補の選び方に使った場合）。</param>
public sealed record PlaceAddedPayload(
    PlaceId Place,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<PlaceLink> Links,
    PlaceStatus Status,
    RngDraws? Draws) : EventPayload(1);

/// <summary>場所の採用（段 3）。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record PlaceAdoptedPayload(PlaceId Place) : EventPayload(1);

/// <summary>場所の廃止（段 3）。</summary>
/// <param name="Place">場所の ID。</param>
public sealed record PlaceRetiredPayload(PlaceId Place) : EventPayload(1);

/// <summary>変換の辞書への記録（段 3）。</summary>
/// <param name="Term">語。</param>
/// <param name="Verdict">判定。</param>
/// <param name="Mapped">変換先（変換のとき）。</param>
/// <param name="Category">構造に載るかの分類。</param>
public sealed record TermRecordedPayload(string Term, TermVerdict Verdict, string? Mapped, string? Category) : EventPayload(1);

/// <summary>状態変数の調整（段 3。窓口から）。</summary>
/// <param name="Vital">状態変数。</param>
/// <param name="Kind">増減か設定か。</param>
/// <param name="Value">値。</param>
/// <param name="Reason">理由（訂正として残す）。</param>
public sealed record VitalAdjustedPayload(VitalName Vital, VitalAdjustKind Kind, double Value, string Reason) : EventPayload(1);

/// <summary>状態の付与（段 3）。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
/// <param name="Duration">持続時間。なければ null。</param>
public sealed record ConditionAttachedPayload(string ConditionId, TimeSpan? Duration) : EventPayload(1);

/// <summary>状態の解除（段 3）。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
public sealed record ConditionDetachedPayload(string ConditionId) : EventPayload(1);

/// <summary>ペルソナの生成（段 3）。</summary>
/// <param name="DefId">ペルソナの定義 ID。</param>
/// <param name="Home">最初の居場所（家）。</param>
public sealed record PersonaCreatedPayload(string DefId, PlaceId Home) : EventPayload(1);

/// <summary>ペルソナの破棄（段 3）。</summary>
public sealed record PersonaDestroyedPayload() : EventPayload(1);

/// <summary>追いつきの上限で飛ばした空白の区間（段 3）。</summary>
/// <param name="From">空白の始まり。</param>
/// <param name="To">空白の終わり。</param>
public sealed record PersonaGapPayload(DateTimeOffset From, DateTimeOffset To) : EventPayload(1);

/// <summary>ワールドの生成（段 3）。</summary>
/// <param name="DefId">ワールドの定義 ID。</param>
/// <param name="Seed">乱数のワールドの種。</param>
public sealed record WorldCreatedPayload(string DefId, ulong Seed) : EventPayload(1);

/// <summary>ワールドの破棄（段 3）。</summary>
public sealed record WorldDestroyedPayload() : EventPayload(1);

/// <summary>定義の読み直し（段 3）。どの定義で動いているかの記録。</summary>
/// <param name="Hash">定義全体のハッシュ。</param>
public sealed record DefinitionsReloadedPayload(string Hash) : EventPayload(1);

/// <summary>体の反応（段 4）。しきい値を越えた。</summary>
/// <param name="Vital">状態変数。</param>
/// <param name="Threshold">しきい値。</param>
/// <param name="StateVersion">状態変数の版。</param>
public sealed record VitalThresholdPayload(VitalName Vital, double Threshold, long StateVersion) : EventPayload(1);

/// <summary>外からの通知の到着（段 5）。受信箱に積む。</summary>
/// <param name="Item">受信箱のアイテム。Seq は適用のときに入る。</param>
public sealed record NotificationArrivedPayload(InboxItem Item) : EventPayload(1);

/// <summary>外向きの操作の送信完了（段 5）。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="ExternalId">送信先での ID（あれば）。</param>
public sealed record OutboundSentPayload(long OperationSeq, string? ExternalId) : EventPayload(1);

/// <summary>外向きの操作の送信失敗（段 5）。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="Reason">失敗の理由。</param>
/// <param name="RetryAfter">次に投稿できる時刻（あれば）。</param>
public sealed record OutboundFailedPayload(long OperationSeq, string Reason, DateTimeOffset? RetryAfter) : EventPayload(1);

/// <summary>外向きの操作の送信結果が不明（段 5）。</summary>
/// <param name="OperationSeq">操作のイベントの連番。</param>
/// <param name="Reason">理由。</param>
public sealed record OutboundUnknownPayload(long OperationSeq, string Reason) : EventPayload(1);

/// <summary>ブロックの開始（段 6）。固定・計画のブロックは ID を持ち、自由時間では ID が null（決めどきを積む）。</summary>
/// <param name="BlockId">ブロックの ID（定義 ID@開始日）。自由時間は null。</param>
/// <param name="PlanVersion">プランの版。</param>
public sealed record BlockStartedPayload(string? BlockId, long PlanVersion) : EventPayload(1);

/// <summary>プランの確定（段 6）。Planner の出力を適用したもの。</summary>
/// <param name="Plan">確定したプラン。</param>
public sealed record PlanCommittedPayload(DayPlan Plan) : EventPayload(1);

/// <summary>プランの作り直しの時刻（段 6）。</summary>
/// <param name="PlanVersion">プランの版。古ければ捨てる。</param>
public sealed record PlanDuePayload(long PlanVersion) : EventPayload(1);

/// <summary>スマホを開く機会（段 6）。</summary>
/// <param name="BlockId">いまのブロックの ID。</param>
public sealed record PhoneOpportunityPayload(string BlockId) : EventPayload(1);

/// <summary>決めどきの要求（段 7）。</summary>
/// <param name="Reason">理由。</param>
/// <param name="BlockId">関係するブロックの ID（あれば）。</param>
public sealed record DecisionRequestedPayload(DecisionReason Reason, string? BlockId) : EventPayload(1);

/// <summary>行動の決定（段 7。Decider の出力）。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">行動する場所。</param>
/// <param name="Route">辺の列。移動がなければ空。</param>
/// <param name="TravelMinutes">移動時間（分）。</param>
/// <param name="Duration">所要時間。</param>
/// <param name="IsSubAction">副行動かどうか（スマホを開く、投稿する）。</param>
/// <param name="Draws">所要時間などに乱数を引いた範囲。</param>
public sealed record ActivityChosenPayload(
    string ActionId,
    PlaceId Place,
    ImmutableArray<RouteStep> Route,
    int TravelMinutes,
    TimeSpan Duration,
    bool IsSubAction,
    RngDraws? Draws) : EventPayload(1);

/// <summary>予定の乱れへの対応の決定（段 7）。</summary>
/// <param name="BlockId">ブロックの ID。</param>
/// <param name="Choice">選んだもの。</param>
/// <param name="Draws">乱数を引いた範囲（遅刻か欠席か待つかを選んだとき）。</param>
public sealed record BlockRespondedPayload(string BlockId, DisruptionChoice Choice, RngDraws? Draws) : EventPayload(1);

/// <summary>移動の開始（段 7）。移動中にする。</summary>
/// <param name="From">出発地。</param>
/// <param name="To">目的地。</param>
/// <param name="Route">辺の列。移動がなければ空。</param>
/// <param name="ArrivesAt">到着の時刻。</param>
/// <param name="ActivityVersion">行動の版。</param>
/// <param name="Next">到着のあとに始める行動（J-134）。</param>
public sealed record TravelStartedPayload(
    PlaceId From,
    PlaceId To,
    ImmutableArray<RouteStep> Route,
    DateTimeOffset ArrivesAt,
    long ActivityVersion,
    PendingAction? Next = null) : EventPayload(1);

/// <summary>行動の開始（段 7）。</summary>
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

/// <summary>言ったこと（発話）の決定（段 7）。「言ったこと」として残る。</summary>
/// <param name="Channel">チャンネルの種類。</param>
/// <param name="Kind">投稿か返信か。</param>
/// <param name="Body">本文。</param>
/// <param name="ReplyTo">返信先（チャンネルが解釈する ID）。</param>
/// <param name="Audience">公開範囲。</param>
/// <param name="BasisSeqs">確定状態への参照（連番の列）。</param>
/// <param name="ChannelPayload">チャンネルが解釈する中身。</param>
/// <param name="Draws">話題や文面の抽選に引いた範囲。</param>
public sealed record OutboundSpeechPayload(
    string Channel,
    OutboundPostKind Kind,
    string Body,
    string? ReplyTo,
    Audience Audience,
    ImmutableArray<long> BasisSeqs,
    JsonElement ChannelPayload,
    RngDraws? Draws) : EventPayload(1);

/// <summary>発話でない外向きの操作の決定（段 7）。「言ったこと」には入らない。</summary>
/// <param name="Channel">チャンネルの種類。</param>
/// <param name="OperationKind">操作の種類（"react"、"follow"、チャンネル固有は "other"）。</param>
/// <param name="ChannelPayload">チャンネルが解釈する中身。</param>
public sealed record OutboundOperationPayload(string Channel, string OperationKind, JsonElement ChannelPayload) : EventPayload(1);

/// <summary>受信箱の捌きの結果（段 7）。</summary>
/// <param name="Outcomes">捌いた通知と、それぞれの結果。</param>
/// <param name="Draws">受信箱の順番などの抽選に引いた範囲。</param>
public sealed record InboxHandledPayload(ImmutableArray<InboxOutcome> Outcomes, RngDraws? Draws) : EventPayload(1);

/// <summary>投稿の空振り（段 7）。話題の選び方などの抽選に引いた範囲を持つ。</summary>
/// <param name="Reason">理由。</param>
/// <param name="Draws">抽選に引いた範囲（引いていなければ null）。</param>
public sealed record PostSkippedPayload(PostSkipReason Reason, RngDraws? Draws) : EventPayload(1);
