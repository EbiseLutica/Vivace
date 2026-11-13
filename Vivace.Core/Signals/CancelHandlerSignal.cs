using Vivace.Core.Domain;

namespace Vivace.Core.Signals;

/// <summary>ペルソナのハンドラを取り消す。出力が返っても捨てる。</summary>
/// <param name="Persona">ペルソナ。</param>
public sealed record CancelHandlerSignal(PersonaId Persona) : EngineSignal;
