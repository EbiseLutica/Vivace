namespace Vivace.Core.Events;

/// <summary>イベントタイプの1件。名前、ステージ、ペイロードの型。</summary>
/// <param name="Name">イベントタイプの名前（"action.started" など）。</param>
/// <param name="Stage">ステージ。</param>
/// <param name="PayloadType">ペイロードの型。</param>
public sealed record EventTypeInfo(string Name, EventStage Stage, Type PayloadType);
