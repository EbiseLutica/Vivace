using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>会話の中の1つの発言。</summary>
/// <param name="Sender">発言者。</param>
/// <param name="IsSelf">自分の発言かどうか。チャンネルが付ける。</param>
/// <param name="Text">本文。</param>
/// <param name="PostedAt">投稿の時刻。</param>
/// <param name="Attachments">添付。</param>
public sealed record ThreadMessage(
    Sender Sender,
    bool IsSelf,
    string? Text,
    DateTimeOffset PostedAt,
    ImmutableArray<Attachment> Attachments);
