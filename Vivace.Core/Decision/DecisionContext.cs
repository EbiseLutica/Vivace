using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;

namespace Vivace.Core.Decision;

/// <summary>決めどきの文脈。写しと now と、決める側が引く乱数の列（位置は写しから始まる）。</summary>
/// <param name="State">全体の状態の写し。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">決めどきの時刻（ホライズンになる）。</param>
/// <param name="Reason">決めどきの理由。</param>
/// <param name="BlockId">関係するブロック（あれば）。</param>
/// <param name="Rules">規則の文脈。</param>
/// <param name="Random">決める側の乱数の列（用途 decide）。</param>
public sealed record DecisionContext(
    SimState State,
    PersonaId Persona,
    DateTimeOffset Now,
    DecisionReason Reason,
    string? BlockId,
    RuleContext Rules,
    IRandom Random);
