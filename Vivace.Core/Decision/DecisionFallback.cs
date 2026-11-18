using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Routes;

namespace Vivace.Core.Decision;

/// <summary>
/// 決め方が例外で失敗したときの代わりの決定（D7 7.1）。乱数は引かない。
/// 自由時間は core.go_to_next（移動があれば移動だけ、なければ次の予定まで待つ）、スマホを開く機会は core.idle（何もしない）にする。
/// </summary>
internal static class DecisionFallback
{
    /// <summary>自由時間の代わりの決定（core.go_to_next）を作る。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナの状態。場所にいること。</param>
    /// <param name="now">決定タイミングの時刻。</param>
    /// <exception cref="InvalidOperationException">ペルソナが場所にいない。</exception>
    public static DeciderOutput FreeTime(RuleContext ctx, SimState state, PersonaState persona, DateTimeOffset now)
    {
        if (persona.Location is not AtPlace at)
        {
            throw new InvalidOperationException("決定タイミングは、ペルソナが場所にいるときにしか起きません。");
        }

        var routes = ctx.Routes.For(state.Worlds[persona.World].DefId);
        var next = persona.Plan?.NextFixedAfter(now);
        var deadline = next?.Start ?? persona.Plan?.HorizonEnd ?? now;
        var place = next?.Place ?? at.Place;
        var minutes = routes.Minutes(at.Place, place);
        if (minutes is null)
        {
            place = at.Place;
            minutes = 0;
        }

        var route = minutes == 0
            ? ImmutableArray<RouteStep>.Empty
            : routes.Steps(at.Place, place) ?? [];
        var wait = minutes == 0 ? deadline - now : TimeSpan.Zero;
        var payload = new ActivityChosenPayload(ActionRules.GoToNextId, place, route, minutes.Value, wait < TimeSpan.Zero ? TimeSpan.Zero : wait, false, null);
        var guard = new Guard(
            [
                new ActivityVersionCheck(persona.ActivityVersion),
                new FeasibleCheck(ActionRules.GoToNextId, place),
            ],
            RejectAction.Redecide);
        return new DeciderOutput(payload, guard);
    }

    /// <summary>スマホを開く機会の代わりの決定（何もしない。所要時間 0 の副行動）を作る。</summary>
    /// <param name="persona">ペルソナの状態。場所にいること。</param>
    /// <exception cref="InvalidOperationException">ペルソナが場所にいない。</exception>
    public static DeciderOutput Phone(PersonaState persona)
    {
        if (persona.Location is not AtPlace at)
        {
            throw new InvalidOperationException("決定タイミングは、ペルソナが場所にいるときにしか起きません。");
        }

        var payload = new ActivityChosenPayload(UtilityDecider.Idle, at.Place, [], 0, TimeSpan.Zero, true, null);
        var guard = new Guard([new ActivityVersionCheck(persona.ActivityVersion)], RejectAction.Redecide);
        return new DeciderOutput(payload, guard);
    }
}
