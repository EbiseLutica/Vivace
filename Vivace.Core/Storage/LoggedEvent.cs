using Vivace.Core.Events;

namespace Vivace.Core.Storage;

/// <summary>適用の記録の1件。行は消さず、中身も書き換えない（D1 1.8）。</summary>
/// <param name="Event">イベント。</param>
/// <param name="Status">結果。</param>
/// <param name="ApplyNo">適用番号（適用したときだけ）。</param>
/// <param name="Reason">捨てた、または却下した理由。</param>
public sealed record LoggedEvent(SimEvent Event, EventStatus Status, long? ApplyNo, string? Reason);
