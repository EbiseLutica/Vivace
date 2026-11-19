using Vivace.Core.Domain;

namespace Vivace.Core.Signals;

/// <summary>プランのハンドラ（Planner の代わり）を、計画のレーンで起動する。ホライズンはトリガーの時刻。</summary>
/// <param name="BatchSeq">出力のまとまりの連番。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">トリガーの時刻。出力の時刻になる。</param>
public sealed record StartPlannerSignal(long BatchSeq, PersonaId Persona, DateTimeOffset Now)
    : StartHandlerSignal(BatchSeq, Persona, Now);
