namespace Vivace.Core.Storage;

/// <summary>イベントの行の状態（events.status）。未適用と処理中は、再起動のときにキューとハンドラを戻す手がかりになる。</summary>
public enum EventRowStatus
{
    /// <summary>未適用。キューにある。</summary>
    Pending,

    /// <summary>ハンドラの出力待ち。StartHandler を出したイベントで、適用番号は振ってある。</summary>
    Processing,

    /// <summary>適用した。処理中のイベントは、出力を取り込んだときにここへ移る。</summary>
    Applied,

    /// <summary>捨てた（古いバージョンなど）。</summary>
    Skipped,

    /// <summary>却下した（guard が通らない）。</summary>
    Rejected,
}
