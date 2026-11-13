using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>状態変数の調整（窓口から）。</summary>
/// <param name="Vital">状態変数。</param>
/// <param name="Kind">増減か設定か。</param>
/// <param name="Value">値。</param>
/// <param name="Reason">理由（訂正として残す）。</param>
public sealed record VitalAdjustedPayload(VitalName Vital, VitalAdjustedPayload.VitalAdjustKind Kind, double Value, string Reason) : EventPayload(1)
{
    /// <summary>状態変数の調整の仕方。</summary>
    public enum VitalAdjustKind
    {
        /// <summary>増減する。</summary>
        Add,

        /// <summary>値を設定する。</summary>
        Set,
    }
}
