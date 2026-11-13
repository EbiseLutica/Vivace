using Vivace.Core.Domain;

namespace Vivace.Core;

/// <summary>ペルソナのハンドラを取り消す。出力が返っても捨てる。</summary>
/// <param name="Persona">ペルソナ。</param>
public sealed record CancelHandler(PersonaId Persona) : EngineSignal;
