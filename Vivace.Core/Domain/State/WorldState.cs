using System.Collections.Immutable;

namespace Vivace.Core.Domain.State;

/// <summary>世界の状態。</summary>
/// <param name="Id">ワールドインスタンスの ID。</param>
/// <param name="DefId">使っている定義の ID。</param>
/// <param name="Seed">乱数のワールドの種。</param>
/// <param name="Status">状態。</param>
/// <param name="GeneratedPlaces">生えた場所（候補、採用、廃止）。</param>
/// <param name="Terms">変換の辞書の、実行中の記録。</param>
/// <param name="Incidents">発生中の出来事。キーは発生のイベントの連番。</param>
/// <param name="GraphVersion">場所のグラフが変わるたびに上がる版。</param>
public sealed record WorldState(
    WorldId Id,
    WorldDefId DefId,
    ulong Seed,
    WorldStatus Status,
    ImmutableDictionary<PlaceId, RuntimePlace> GeneratedPlaces,
    ImmutableDictionary<string, TermEntry> Terms,
    ImmutableDictionary<long, ActiveIncident> Incidents,
    long GraphVersion);
