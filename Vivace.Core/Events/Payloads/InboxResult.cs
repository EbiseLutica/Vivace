namespace Vivace.Core.Events;

/// <summary>受信箱の1件の捌きの結果。</summary>
public enum InboxResult
{
    /// <summary>返信した。</summary>
    Reply,

    /// <summary>リアクションした。</summary>
    React,

    /// <summary>フォローし返した。</summary>
    FollowBack,

    /// <summary>見ただけ。</summary>
    Seen,

    /// <summary>何もしない。</summary>
    Nothing,
}
