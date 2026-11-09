using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;

namespace Vivace.Core.Ingress;

/// <summary>ワールドとペルソナの生成のイベントを作る窓口。</summary>
public static class Ingress
{
    /// <summary>ワールドの生成のイベントを作る。</summary>
    /// <param name="at">時刻（UTC）。</param>
    /// <param name="defId">ワールドの定義 ID。</param>
    /// <param name="seed">乱数のワールドの種。</param>
    public static NewEvent WorldCreated(DateTimeOffset at, string defId, ulong seed) =>
        new(at, EventTypes.WorldCreated, new WorldId(defId), null, new WorldCreatedPayload(defId, seed));

    /// <summary>ペルソナの生成のイベントを作る。</summary>
    /// <param name="at">時刻（UTC）。</param>
    /// <param name="worldDefId">ペルソナの居住するワールドの定義 ID。</param>
    /// <param name="personaDefId">ペルソナの定義 ID。</param>
    /// <param name="home">最初の居場所（家）。</param>
    public static NewEvent PersonaCreated(DateTimeOffset at, string worldDefId, string personaDefId, PlaceId home) =>
        new(at, EventTypes.PersonaCreated, new WorldId(worldDefId), null, new PersonaCreatedPayload(personaDefId, home));
}
