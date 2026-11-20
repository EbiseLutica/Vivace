using Vivace.Core.Events;

namespace Vivace.Core.Storage;

/// <summary>events の行の1件（読み出し）。</summary>
/// <param name="Event">イベント。</param>
/// <param name="Status">行の状態。</param>
/// <param name="ApplyNo">適用番号（適用したもの、処理中のものだけ）。</param>
/// <param name="Reason">捨てた、または却下した理由。</param>
public sealed record EventRow(SimEvent Event, EventRowStatus Status, long? ApplyNo, string? Reason);
