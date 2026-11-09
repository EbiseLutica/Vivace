using System.Text.Json;

namespace Vivace.Core.Events;

/// <summary>発話でない外向きの操作の決定（段 7）。「言ったこと」には入らない。</summary>
/// <param name="Channel">チャンネルの種類。</param>
/// <param name="OperationKind">操作の種類（"react"、"follow"、チャンネル固有は "other"）。</param>
/// <param name="ChannelPayload">チャンネルが解釈する中身。</param>
public sealed record OutboundOperationPayload(string Channel, string OperationKind, JsonElement ChannelPayload) : EventPayload(1);
