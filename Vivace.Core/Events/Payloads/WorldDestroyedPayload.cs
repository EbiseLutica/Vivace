namespace Vivace.Core.Events;

/// <summary>ワールドの破棄（段 3）。</summary>
public sealed record WorldDestroyedPayload() : EventPayload(1);
