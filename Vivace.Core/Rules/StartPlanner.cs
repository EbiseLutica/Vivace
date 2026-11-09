using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>プランのハンドラ（Planner の代わり）を、計画のレーンで起動する。</summary>
/// <param name="BatchKey">出力のまとまりの鍵。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">契機の時刻。</param>
public sealed record StartPlanner(long BatchKey, PersonaId Persona, DateTimeOffset Now) : EngineSignal;
