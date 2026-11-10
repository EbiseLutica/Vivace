using Vivace.Core.Events;

namespace Vivace.Core.Domain;

/// <summary>適用の位置。最後に適用したイベントの時刻、段、連番と、適用番号。</summary>
/// <param name="Time">時刻。</param>
/// <param name="Stage">段。</param>
/// <param name="Seq">連番。</param>
/// <param name="ApplyNo">適用番号。</param>
public readonly record struct Position(DateTimeOffset Time, EventStage Stage, long Seq, long ApplyNo);
