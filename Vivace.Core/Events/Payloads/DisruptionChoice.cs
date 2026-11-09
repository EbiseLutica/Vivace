namespace Vivace.Core.Events;

/// <summary>予定の乱れへの対応の選択。</summary>
public enum DisruptionChoice
{
    /// <summary>遅刻する。</summary>
    Late,

    /// <summary>欠席する。</summary>
    Absent,

    /// <summary>待つ。</summary>
    Wait,
}
