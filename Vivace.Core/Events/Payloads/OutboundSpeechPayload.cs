using System.Collections.Immutable;
using System.Text.Json;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>言ったこと（発話）の決定。「言ったこと」として残る。</summary>
/// <param name="Channel">チャンネルの種類。</param>
/// <param name="Kind">投稿か返信か。</param>
/// <param name="Body">本文。</param>
/// <param name="ReplyTo">返信先（チャンネルが解釈する ID）。</param>
/// <param name="Audience">公開範囲。</param>
/// <param name="BasisSeqs">確定状態への参照（連番の列）。</param>
/// <param name="ChannelPayload">チャンネルが解釈する中身。</param>
/// <param name="Draws">話題や文面の抽選に引いた範囲。</param>
public sealed record OutboundSpeechPayload(
    string Channel,
    OutboundPostKind Kind,
    string Body,
    string? ReplyTo,
    Audience Audience,
    ImmutableArray<long> BasisSeqs,
    JsonElement ChannelPayload,
    RngDraws? Draws) : EventPayload(1);
