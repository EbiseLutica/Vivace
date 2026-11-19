using System.Text.Json;
using System.Text.Json.Nodes;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.Domain.State;

/// <summary>未送信の外向きの操作。</summary>
/// <param name="Seq">操作のイベントの連番（ID を兼ねる）。</param>
/// <param name="Channel">チャンネルの種類（"misskey" など）。</param>
/// <param name="OperationKind">操作の種類。コアの語彙は "react" と "follow"、ほかは "other"。発話は "post" か "reply"。</param>
/// <param name="IsSpeech">言ったこと（発話）かどうか。</param>
/// <param name="Payload">チャンネルだけが読む中身。</param>
public sealed record OutboundOp(
    long Seq,
    string Channel,
    string OperationKind,
    bool IsSpeech,
    JsonElement Payload)
{
    /// <summary>発話から未送信の操作を作る。中身は本文、返信先、公開範囲、チャンネルの中身。</summary>
    /// <param name="seq">発話のイベントの連番。</param>
    /// <param name="speech">発話の中身。</param>
    public static OutboundOp FromSpeech(long seq, OutboundSpeechPayload speech)
    {
        var body = new JsonObject
        {
            ["body"] = speech.Body,
            ["replyTo"] = speech.ReplyTo,
            ["audience"] = speech.Audience.ToString(),
            ["channelPayload"] = speech.ChannelPayload.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(speech.ChannelPayload.GetRawText()),
        };
        var kind = speech.Kind == OutboundSpeechPayload.OutboundPostKind.Reply ? "reply" : "post";
        return new OutboundOp(seq, speech.Channel, kind, true, JsonSerializer.SerializeToElement(body));
    }
}
