namespace Vivace.Core.Events;

/// <summary>イベントタイプの1件。名前、段、ペイロードの型。</summary>
/// <param name="Name">イベントタイプの名前（"action.started" など）。</param>
/// <param name="Stage">段。</param>
/// <param name="PayloadType">ペイロードの型。</param>
public sealed record EventTypeInfo(string Name, EventStage Stage, Type PayloadType);
