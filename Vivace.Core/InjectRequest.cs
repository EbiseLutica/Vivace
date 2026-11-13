using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core;

/// <summary>投入の窓口への要求。投入するイベントの型と中身、時刻の指定、出所を持つ。</summary>
/// <param name="EventType">イベントタイプの名前。</param>
/// <param name="Payload">中身。</param>
/// <param name="At">時刻（UTC）。null なら、窓口が受け付けた時点の時計の時刻。</param>
/// <param name="Origin">出所。</param>
public sealed record InjectRequest(string EventType, EventPayload Payload, DateTimeOffset? At, Origin Origin)
{
    /// <summary>ワールドの生成の要求を作る。インスタンスの ID は適用のときに決める。</summary>
    /// <param name="defId">ワールドの定義 ID。</param>
    /// <param name="seed">乱数のワールドの種。</param>
    /// <param name="origin">出所。</param>
    /// <param name="at">時刻（UTC）。省略すると、受け付けた時点の時計の時刻。</param>
    public static InjectRequest WorldCreated(WorldDefId defId, ulong seed, Origin origin, DateTimeOffset? at = null) =>
        new(EventTypes.WorldCreated, new WorldCreatedPayload(defId, seed), at, origin);

    /// <summary>ペルソナの生成の要求を作る。居住するワールドのインスタンスは適用のときに引く。</summary>
    /// <param name="personaDefId">ペルソナの定義 ID。</param>
    /// <param name="home">最初の居場所（家）。</param>
    /// <param name="origin">出所。</param>
    /// <param name="at">時刻（UTC）。省略すると、受け付けた時点の時計の時刻。</param>
    public static InjectRequest PersonaCreated(PersonaDefId personaDefId, PlaceId home, Origin origin, DateTimeOffset? at = null) =>
        new(EventTypes.PersonaCreated, new PersonaCreatedPayload(personaDefId, home), at, origin);
}
