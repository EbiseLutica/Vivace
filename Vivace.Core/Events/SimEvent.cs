using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>
/// イベントの出所の大分類（D2 2.9）。
/// </summary>
public enum OriginKind
{
    /// <summary>通常の運転で、Apply かハンドラが積んだ。</summary>
    Sim,

    /// <summary>追いつきの間に積んだ。</summary>
    Catchup,

    /// <summary>管理者が窓口から投入した。</summary>
    Admin,

    /// <summary>テストやドライランで注入した。</summary>
    Test,

    /// <summary>チャンネルの到着（名前がチャンネルの種類）。</summary>
    Channel,

    /// <summary>プラグインが窓口から投入した（名前がプラグインの名前）。</summary>
    Plugin,
}

/// <summary>イベントの出所。文字列にすると "sim"、"channel:misskey" の形になる。</summary>
/// <param name="Kind">大分類。</param>
/// <param name="Name">チャンネルかプラグインの名前。それ以外は null。</param>
public readonly record struct Origin(OriginKind Kind, string? Name = null)
{
    /// <summary>通常の運転の出所。</summary>
    public static Origin Sim => new(OriginKind.Sim);

    /// <summary>追いつきの出所。</summary>
    public static Origin Catchup => new(OriginKind.Catchup);

    /// <summary>テストの出所。</summary>
    public static Origin Test => new(OriginKind.Test);

    /// <summary>管理者の出所。</summary>
    public static Origin Admin => new(OriginKind.Admin);

    /// <summary>チャンネルの到着の出所。</summary>
    /// <param name="channel">チャンネルの種類。</param>
    public static Origin FromChannel(string channel) => new(OriginKind.Channel, channel);

    /// <summary>プラグインの出所。</summary>
    /// <param name="plugin">プラグインの名前。</param>
    public static Origin FromPlugin(string plugin) => new(OriginKind.Plugin, plugin);

    /// <summary>出所を文字列にする（"sim"、"channel:misskey" など）。</summary>
    public override string ToString() => Kind switch
    {
        OriginKind.Sim => "sim",
        OriginKind.Catchup => "catchup",
        OriginKind.Admin => "admin",
        OriginKind.Test => "test",
        OriginKind.Channel => $"channel:{Name}",
        OriginKind.Plugin => $"plugin:{Name}",
        _ => Kind.ToString(),
    };
}

/// <summary>
/// シミュレーションのイベント（D2 2.1）。ID は連番と兼ねる（P-10）。段は持たず、イベントタイプから引く。
/// </summary>
/// <param name="Seq">連番。ID を兼ねる。</param>
/// <param name="Time">絶対時刻（UTC）。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="WorldId">ワールド。関係しなければ null。</param>
/// <param name="PersonaId">ペルソナ。関係しなければ null。</param>
/// <param name="Payload">イベントタイプごとの中身。決めた結果もここに入る。</param>
/// <param name="Guard">適用前の検査。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（契機の決めどきの連番）。</param>
/// <param name="CauseSeq">このイベントを生んだイベントの連番。</param>
/// <param name="Origin">出所。</param>
/// <param name="Trace">痕跡。Apply は読まない。</param>
/// <param name="RealTime">実時刻（送信完了、外部からの元の到着）。</param>
public sealed record SimEvent(
    long Seq,
    DateTimeOffset Time,
    string EventType,
    WorldId? WorldId,
    PersonaId? PersonaId,
    EventPayload Payload,
    Guard? Guard,
    long? BatchSeq,
    long? CauseSeq,
    Origin Origin,
    JsonElement? Trace,
    DateTimeOffset? RealTime)
{
    /// <summary>イベントタイプから引いた段。</summary>
    public EventStage Stage => EventTypes.StageOf(EventType);
}

/// <summary>
/// 適用に失敗したときの動き（D1 1.7）。
/// </summary>
public enum RejectAction
{
    /// <summary>同じ時刻（いま）に決めどきを積み直す。</summary>
    Redecide,

    /// <summary>文面の作り直しを積む（辞書の食い違い）。</summary>
    Recompose,

    /// <summary>何もしない（割り込みで不要になった場合）。</summary>
    Drop,
}

/// <summary>
/// 適用してよい条件（guard）。決める側の出力に付け、Apply が直前に評価する（D1 1.7）。
/// </summary>
/// <param name="Checks">確かめることの列。すべて通ったときだけ適用する。</param>
/// <param name="OnReject">却下されたときの動き。</param>
public sealed record Guard(ImmutableArray<GuardCheck> Checks, RejectAction OnReject);

/// <summary>guard の1つの検査。</summary>
public abstract record GuardCheck;

/// <summary>行動の版が、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときの行動の版。</param>
public sealed record ActivityVersionCheck(long Expected) : GuardCheck;

/// <summary>選んだ行動と場所が、いまの状態でも実行できること。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">場所の ID。</param>
public sealed record FeasibleCheck(string ActionId, PlaceId Place) : GuardCheck;

/// <summary>乱数の列の位置が、決めたときと同じであること。</summary>
/// <param name="Owner">持ち主（ペルソナのインスタンス ID）。</param>
/// <param name="Purpose">用途（"decide"、"sns"）。</param>
/// <param name="Position">決めたときの位置。</param>
public sealed record RngCursorCheck(string Owner, string Purpose, ulong Position) : GuardCheck;

/// <summary>プランの版が、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときのプランの版。</param>
public sealed record PlanVersionCheck(long Expected) : GuardCheck;

/// <summary>変換の辞書に、その語がまだないこと（または同じ変換であること）。</summary>
/// <param name="Term">語。</param>
public sealed record TermUnsetCheck(string Term) : GuardCheck;

/// <summary>同じ名前の場所が、まだないこと。</summary>
/// <param name="Name">場所の名前。</param>
public sealed record PlaceUnsetCheck(string Name) : GuardCheck;
