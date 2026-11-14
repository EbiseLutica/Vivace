using System.Collections.Immutable;
using System.Text.Json;

namespace Vivace.Core.Domain;

/// <summary>
/// 受信箱のアイテム。チャンネルが届いた通知を、コアが読める形に訳したもの。
/// </summary>
/// <param name="Seq">到着のイベントの連番（ID を兼ねる）。受け付けたときは 0 で、適用のときに入る。</param>
/// <param name="Channel">チャンネルの種類（"misskey" など）。</param>
/// <param name="Kind">大分類。</param>
/// <param name="ChannelType">チャンネル独自の種類（"mention"、"reply" など）。</param>
/// <param name="Sender">送り主。</param>
/// <param name="Audience">公開範囲。</param>
/// <param name="ConversationKey">同じ会話を見分けるキー。</param>
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
