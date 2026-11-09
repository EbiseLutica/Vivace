namespace Vivace.Core.Domain;

/// <summary>未送信の外向きの操作。</summary>
/// <param name="Seq">操作のイベントの連番（ID を兼ねる）。</param>
/// <param name="Channel">チャンネルの種類（"misskey" など）。</param>
/// <param name="OperationKind">操作の種類。コアの語彙は "react" と "follow"、ほかは "other"。</param>
/// <param name="IsSpeech">言ったこと（発話）かどうか。</param>
/// <param name="Payload">チャンネルだけが読む中身。</param>
public sealed record OutboundOp(
    long Seq,
    string Channel,
    string OperationKind,
    bool IsSpeech,
    System.Text.Json.JsonElement Payload);
