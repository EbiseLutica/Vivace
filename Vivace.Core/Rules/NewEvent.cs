using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules;

/// <summary>後続のイベント。Apply と同じトランザクションでキューに入る。</summary>
/// <param name="Time">時刻。</param>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="World">ワールド。</param>
/// <param name="Persona">ペルソナ。</param>
/// <param name="Payload">中身。</param>
/// <param name="Guard">適用前の検査（決める側の出力のとき）。</param>
/// <param name="BatchSeq">同じハンドラの出力のまとまり（決める側の出力のとき）。</param>
/// <param name="Origin">出所。null なら契機の出所を引き継ぐ。</param>
public sealed record NewEvent(
    DateTimeOffset Time,
    string EventType,
    WorldId? World,
    PersonaId? Persona,
    EventPayload Payload,
    Guard? Guard = null,
    long? BatchSeq = null,
    Origin? Origin = null);
