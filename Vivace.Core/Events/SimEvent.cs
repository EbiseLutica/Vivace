using System.Text.Json;
using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>シミュレーションのイベント。</summary>
/// <param name="Seq">連番。ID を兼ねる。</param>
/// <param name="Time">絶対時刻（UTC）。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="WorldId">ワールド。関係しなければ null。</param>
/// <param name="PersonaId">ペルソナ。関係しなければ null。</param>
/// <param name="Payload">イベントタイプごとの中身。決めた結果もここに入る。</param>
/// <param name="Guard">適用前の検査。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（契機の決めどきの連番）。</param>
/// <param name="CauseSeq">このイベントを生んだイベントの連番。</param>
/// <param name="Origin">出所。</param>
/// <param name="Trace">痕跡。Apply は読まない。</param>
/// <param name="RealTime">実時刻（送信完了、外部からの元の到着）。</param>
public sealed record SimEvent(
    long Seq,
    DateTimeOffset Time,
    string EventType,
    WorldId? WorldId,
    PersonaId? PersonaId,
    EventPayload Payload,
    Guard? Guard,
    long? BatchSeq,
    long? CauseSeq,
    Origin Origin,
    JsonElement? Trace,
    DateTimeOffset? RealTime)
{
    /// <summary>イベントタイプから引いた段。</summary>
    public EventStage Stage => EventTypes.StageOf(EventType);
}
