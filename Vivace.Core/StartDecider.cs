using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core;

/// <summary>決めどきのハンドラ（Decider）を、レーンで起動する。</summary>
/// <param name="BatchKey">出力のまとまりの鍵（決めどきの連番）。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">契機の時刻。ホライズンになる。</param>
/// <param name="Reason">決めどきの理由。</param>
/// <param name="BlockId">関係するブロック。</param>
public sealed record StartDecider(long BatchKey, PersonaId Persona, DateTimeOffset Now, DecisionReason Reason, string? BlockId) : EngineSignal;
