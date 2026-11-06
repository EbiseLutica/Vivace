using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;

namespace Vivace.Core.Ingress;

/// <summary>
/// 窓口（最小）。ワールドとペルソナの生成のイベントを作る（D2 2.8）。
/// 段階2 は、生成だけを受け付ける。定義の確かめと、ほかの投入は段階3 で足す。
/// </summary>
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
