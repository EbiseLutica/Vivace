using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.Decision;

/// <summary>決める側の出力。activity.chosen の中身と、適用してよい条件。</summary>
/// <param name="Payload">行動の決定。</param>
/// <param name="Guard">適用前の検査。</param>
public sealed record DeciderOutput(ActivityChosenPayload Payload, Guard Guard);
