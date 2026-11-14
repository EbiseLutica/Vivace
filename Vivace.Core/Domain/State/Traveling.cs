using System.Collections.Immutable;

namespace Vivace.Core.Domain.State;

/// <summary>移動中。どの場所にもいないものとして扱う。</summary>
/// <param name="From">出発地。</param>
/// <param name="To">目的地。</param>
/// <param name="Route">経路（区間の列）。出発時に確定し、途中で区間が伸びたら残りを積み直す。</param>
/// <param name="DepartedAt">出発の時刻。</param>
/// <param name="ArrivesAt">到着の時刻。</param>
/// <param name="Next">到着のあとに始める行動。なければ null。</param>
public sealed record Traveling(
    PlaceId From,
    PlaceId To,
    ImmutableArray<RouteStep> Route,
    DateTimeOffset DepartedAt,
    DateTimeOffset ArrivesAt,
    PendingAction? Next = null) : Location;
