namespace Vivace.Core.Events;

/// <summary>投稿が空振りした理由。</summary>
public enum PostSkipReason
{
    /// <summary>話題が全部却下された。</summary>
    AllRejected,

    /// <summary>文面の生成に失敗した。</summary>
    GenerationFailed,

    /// <summary>締め切りに間に合わなかった。</summary>
    Deadline,
}
