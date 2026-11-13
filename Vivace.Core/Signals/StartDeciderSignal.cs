using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Signals;

/// <summary>決めどきのハンドラ（Decider）を、レーンで起動する。</summary>
/// <param name="BatchSeq">出力のまとまりの連番（決めどきの連番と同じ）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">契機の時刻。ホライズンになる。</param>
/// <param name="Reason">決めどきの理由。</param>
/// <param name="BlockId">関係するブロック。</param>
public sealed record StartDeciderSignal(long BatchSeq, PersonaId Persona, DateTimeOffset Now, DecisionReason Reason, string? BlockId) : EngineSignal;
