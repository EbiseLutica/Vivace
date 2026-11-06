using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Routes;
using Vivace.Core.Time;

namespace Vivace.Core.Rules;

/// <summary>
/// 規則の文脈。定義と、ワールドごとの最短経路の表。どちらも定義が変わるまで不変。
/// </summary>
/// <param name="Defs">読み込んだ定義。</param>
/// <param name="Routes">ワールドごとの最短経路の表。</param>
/// <param name="Holidays">祝日の暦（P-18）。省略すると祝日のない暦。</param>
public sealed record RuleContext(Definitions.Definitions Defs, RouteCache Routes, IHolidayCalendar Holidays)
{
    /// <summary>祝日の暦を省略した文脈。</summary>
    public RuleContext(Definitions.Definitions defs, RouteCache routes) : this(defs, routes, NoHolidays.Instance)
    {
    }
}

/// <summary>イベントの適用の結果。</summary>
public enum EventOutcome
{
    /// <summary>適用した。</summary>
    Applied,

    /// <summary>版が古い、または対象がないので捨てた。</summary>
    Skipped,

    /// <summary>guard が通らなかった。</summary>
    Rejected,
}

/// <summary>
/// 後続のイベント（決定的に導けるもの）。Apply と同じトランザクションでキューに入る。
/// 出す者の出所と、契機の連番は、エンジンが付ける。
/// </summary>
/// <param name="Time">時刻。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="World">ワールド。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Payload">中身。</param>
/// <param name="Guard">適用前の検査（決める側の出力のとき）。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（決める側の出力のとき）。</param>
/// <param name="Origin">出所。null なら契機の出所を引き継ぐ。</param>
public sealed record NewEvent(
    DateTimeOffset Time,
    string EventType,
    WorldId? World,
    PersonaId? Persona,
    EventPayload Payload,
    Guard? Guard = null,
    long? BatchSeq = null,
    Origin? Origin = null);

/// <summary>エンジンへの依頼（データ）。コミットのあとにエンジンが実行する（D1 1.3）。</summary>
public abstract record EngineSignal;

/// <summary>決めどきのハンドラ（Decider）を、レーンで起動する。</summary>
/// <param name="BatchKey">出力のまとまりの鍵（決めどきの連番）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">契機の時刻。ホライズンになる。</param>
/// <param name="Reason">決めどきの理由。</param>
/// <param name="BlockId">関係するブロック。</param>
public sealed record StartDecider(long BatchKey, PersonaId Persona, DateTimeOffset Now, DecisionReason Reason, string? BlockId) : EngineSignal;

/// <summary>プランのハンドラ（Planner の代わり）を、計画のレーンで起動する。</summary>
/// <param name="BatchKey">出力のまとまりの鍵。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">契機の時刻。</param>
public sealed record StartPlanner(long BatchKey, PersonaId Persona, DateTimeOffset Now) : EngineSignal;

/// <summary>ペルソナのハンドラを取り消す（割り込みで行動が打ち切られた）。出力が返っても捨てる（D1 1.3 の P-03）。</summary>
/// <param name="Persona">ペルソナ。</param>
public sealed record CancelHandler(PersonaId Persona) : EngineSignal;

/// <summary>
/// イベントの適用の結果。状態は、適用したときだけ変わる（却下のときは決め中の印を下ろすだけのことがある）。
/// </summary>
/// <param name="State">新しい状態。</param>
/// <param name="Outcome">結果。</param>
/// <param name="Reason">捨てた、または却下した理由（適用のときは null）。</param>
/// <param name="FollowUps">後続のイベント。</param>
/// <param name="Signals">エンジンへの依頼。</param>
public sealed record ApplyResult(
    SimState State,
    EventOutcome Outcome,
    string? Reason,
    ImmutableArray<NewEvent> FollowUps,
    ImmutableArray<EngineSignal> Signals)
{
    /// <summary>適用した結果を作る。</summary>
    public static ApplyResult Applied(SimState state, ImmutableArray<NewEvent> followUps = default, ImmutableArray<EngineSignal> signals = default) =>
        new(state, EventOutcome.Applied, null, followUps.IsDefault ? ImmutableArray<NewEvent>.Empty : followUps, signals.IsDefault ? ImmutableArray<EngineSignal>.Empty : signals);

    /// <summary>捨てた結果を作る。状態は変えない。</summary>
    public static ApplyResult Skipped(SimState state, string reason) => new(state, EventOutcome.Skipped, reason, [], []);
}
