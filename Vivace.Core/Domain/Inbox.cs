using System.Collections.Immutable;
using System.Text.Json;

namespace Vivace.Core.Domain;

/// <summary>受信箱のアイテムの大分類。コアはこれで捌き方を決める（D6 6.5）。</summary>
public enum InboxKind
{
    /// <summary>言葉で話しかけられた（メンション、返信、引用、DM）。</summary>
    Message,

    /// <summary>自分の発言へのリアクション。</summary>
    Reaction,

    /// <summary>フォローされた。</summary>
    Follow,

    /// <summary>上のどれにも当たらない、チャンネル固有のもの。</summary>
    Other,
}

/// <summary>会話の公開範囲。</summary>
public enum Audience
{
    /// <summary>公開。</summary>
    Public,

    /// <summary>DM。</summary>
    Dm,

    /// <summary>記憶にしない。</summary>
    NoMemory,
}

/// <summary>送り主。</summary>
/// <param name="UserId">チャンネル上のユーザー ID。</param>
/// <param name="DisplayName">表示名。</param>
/// <param name="Handle">ハンドル名（あれば）。</param>
public sealed record Sender(string UserId, string DisplayName, string? Handle);

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

/// <summary>添付。</summary>
/// <param name="Kind">種類（"image" など）。</param>
/// <param name="Url">URL。</param>
/// <param name="Description">説明（あれば）。</param>
public sealed record Attachment(string Kind, string Url, string? Description);

/// <summary>
/// 受信箱のアイテム。チャンネルが届いた通知を、コアが読める形に訳したもの（D3 3.7.1）。
/// </summary>
/// <param name="Seq">到着のイベントの連番（ID を兼ねる）。受け付けたときは 0 で、適用のときに入る。</param>
/// <param name="Channel">チャンネルの種類（"misskey" など）。</param>
/// <param name="Kind">大分類。</param>
/// <param name="ChannelType">チャンネル独自の種類（"mention"、"reply" など）。</param>
/// <param name="Sender">送り主。</param>
/// <param name="Audience">公開範囲。</param>
/// <param name="ConversationKey">同じ会話を見分ける鍵。</param>
/// <param name="Text">本文（Message）。</param>
/// <param name="Attachments">添付（Message）。</param>
/// <param name="Thread">それまでの会話（古い順、直近 20 件まで）。</param>
/// <param name="Reaction">リアクションの絵文字（Reaction）。</param>
/// <param name="TargetExternalId">リアクションされた自分の発言の外部 ID（Reaction）。</param>
/// <param name="OriginalArrivedAt">元の到着時刻（圏外明けに取り直したとき）。</param>
/// <param name="Payload">チャンネルだけが読む中身。</param>
public sealed record InboxItem(
    long Seq,
    string Channel,
    InboxKind Kind,
    string ChannelType,
    Sender Sender,
    Audience Audience,
    string? ConversationKey,
    string? Text,
    ImmutableArray<Attachment> Attachments,
    ImmutableArray<ThreadMessage> Thread,
    string? Reaction,
    string? TargetExternalId,
    DateTimeOffset? OriginalArrivedAt,
    JsonElement Payload);
