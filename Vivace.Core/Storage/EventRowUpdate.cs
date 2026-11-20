namespace Vivace.Core.Storage;

/// <summary>既存の events の行の状態を変える（書き込み）。適用番号は、振るときだけ入れる。</summary>
/// <param name="Seq">対象のイベントの連番。</param>
/// <param name="Status">新しい状態。</param>
/// <param name="ApplyNo">振る適用番号。振らないなら null（既存の番号は残す）。</param>
/// <param name="Reason">捨てた、または却下した理由。</param>
public sealed record EventRowUpdate(long Seq, EventRowStatus Status, long? ApplyNo, string? Reason);
