using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;

namespace Vivace.Core.Decision;

/// <summary>決定タイミングのコンテキスト。スナップショットと now と、決める側が引く乱数の列（位置はスナップショットから始まる）。</summary>
/// <param name="State">全体の状態のスナップショット。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Now">決定タイミングの時刻（ホライズンになる）。</param>
/// <param name="Reason">決定タイミングの理由。</param>
/// <param name="BlockId">関係するブロック（あれば）。</param>
/// <param name="Rules">規則のコンテキスト。</param>
/// <param name="Random">決める側の乱数の列（用途 decide）。</param>
public sealed record DecisionContext(
    SimState State,
    PersonaId Persona,
    DateTimeOffset Now,
    DecisionReason Reason,
    string? BlockId,
    RuleContext Rules,
    IRandom Random);
