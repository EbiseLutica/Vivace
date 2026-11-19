using Vivace.Core.Domain;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Signals;

/// <summary>未送信の外向きの操作を、配信役に渡す。</summary>
/// <param name="Persona">操作のペルソナ。</param>
/// <param name="Operation">未送信の操作。</param>
public sealed record DeliverSignal(PersonaId Persona, OutboundOp Operation) : EngineSignal;
