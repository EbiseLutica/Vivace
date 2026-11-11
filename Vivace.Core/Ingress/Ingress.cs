using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;

namespace Vivace.Core.Ingress;

/// <summary>ワールドとペルソナの生成のイベントを作る窓口。</summary>
public static class Ingress
{
    /// <summary>ワールドの生成のイベントを作る。インスタンスの ID は適用のときに決める。</summary>
    /// <param name="at">時刻（UTC）。</param>
    /// <param name="defId">ワールドの定義 ID。</param>
    /// <param name="seed">乱数のワールドの種。</param>
    public static NewEvent WorldCreated(DateTimeOffset at, WorldDefId defId, ulong seed) =>
        new(at, EventTypes.WorldCreated, null, null, new WorldCreatedPayload(defId, seed));

    /// <summary>ペルソナの生成のイベントを作る。居住するワールドのインスタンスは適用のときに引く。</summary>
    /// <param name="at">時刻（UTC）。</param>
    /// <param name="personaDefId">ペルソナの定義 ID。</param>
    /// <param name="home">最初の居場所（家）。</param>
    public static NewEvent PersonaCreated(DateTimeOffset at, PersonaDefId personaDefId, PlaceId home) =>
        new(at, EventTypes.PersonaCreated, null, null, new PersonaCreatedPayload(personaDefId, home));
}
