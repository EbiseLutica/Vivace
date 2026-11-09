namespace Vivace.Core.Events;

/// <summary>ペルソナの破棄（段 3）。</summary>
public sealed record PersonaDestroyedPayload() : EventPayload(1);
