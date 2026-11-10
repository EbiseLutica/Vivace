namespace Vivace.Core.Events;

/// <summary>外向きの投稿の種類。</summary>
public enum OutboundPostKind
{
    /// <summary>新しい投稿。</summary>
    Post,

    /// <summary>返信。</summary>
    Reply,
}
