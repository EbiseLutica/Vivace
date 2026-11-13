namespace Vivace.Core;

/// <summary>投入の結果。受け付けたら連番を、拒否したら理由を持つ。</summary>
/// <param name="Seq">受け付けたイベントの連番。拒否なら null。</param>
/// <param name="Reason">拒否の理由。受け付けたら null。</param>
public sealed record InjectResult(long? Seq, string? Reason)
{
    /// <summary>受け付けたかどうか。</summary>
    public bool Accepted => Seq is not null;

    /// <summary>受け付けた結果を作る。</summary>
    /// <param name="seq">受け付けたイベントの連番。</param>
    public static InjectResult Accept(long seq) => new(seq, null);

    /// <summary>拒否した結果を作る。</summary>
    /// <param name="reason">拒否の理由。</param>
    public static InjectResult Reject(string reason) => new(null, reason);
}
