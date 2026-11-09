using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>ペルソナのハンドラを取り消す（割り込みで行動が打ち切られた）。出力が返っても捨てる（D1 1.3 の P-03）。</summary>
/// <param name="Persona">ペルソナ。</param>
public sealed record CancelHandler(PersonaId Persona) : EngineSignal;
