using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Routes;

namespace Vivace.Core.Decision;

/// <summary>次の予定の場所へ向かう行動（core.go_to_next）の、場所、移動時間（分）、待ち時間、ルート。</summary>
/// <param name="Place">向かう場所。移動できなければ、いまの場所。</param>
/// <param name="Minutes">移動時間（分）。</param>
/// <param name="Wait">移動がないときの待ち時間。移動があるときは 0。</param>
/// <param name="Route">ルート。移動がなければ空。</param>
internal sealed record GoToNextTarget(PlaceId Place, int Minutes, TimeSpan Wait, ImmutableArray<RouteStep> Route)
{
    /// <summary>次の予定の場所へ向かう行動の内容を求める。移動できない場所なら、いまの場所で待つ。</summary>
    /// <param name="routes">ワールドの経路。</param>
    /// <param name="here">いまの場所。</param>
    /// <param name="next">次の固定ブロック（なければ null）。</param>
    /// <param name="now">いまの時刻。</param>
    /// <param name="deadline">待ちの終わり（次の予定の開始、またはプランの終わり）。</param>
    public static GoToNextTarget Of(RouteTable routes, PlaceId here, PlanBlock? next, DateTimeOffset now, DateTimeOffset deadline)
    {
        var place = next?.Place ?? here;
        var minutes = routes.Minutes(here, place);
        if (minutes is null)
        {
            place = here;
            minutes = 0;
        }

        var route = minutes == 0
            ? ImmutableArray<RouteStep>.Empty
            : routes.Steps(here, place) ?? [];
        var wait = minutes == 0 ? deadline - now : TimeSpan.Zero;
        return new GoToNextTarget(place, minutes.Value, wait, route);
    }
}
