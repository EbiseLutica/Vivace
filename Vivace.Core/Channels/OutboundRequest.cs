using System.Text.Json;
using Vivace.Core.Domain;

namespace Vivace.Core.Channels;

/// <summary>チャンネルに渡す、未送信の外向きの操作。</summary>
/// <param name="Seq">操作の連番（ID）。</param>
/// <param name="Persona">送るペルソナ。</param>
/// <param name="At">送信を始める時刻（時計の「いま」）。</param>
/// <param name="OperationKind">操作の種類（"post"、"reply" など）。</param>
/// <param name="IsSpeech">言ったこと（発話）かどうか。</param>
/// <param name="Payload">チャンネルが読む中身。</param>
public sealed record OutboundRequest(long Seq, PersonaId Persona, DateTimeOffset At, string OperationKind, bool IsSpeech, JsonElement Payload);
