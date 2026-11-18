using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.Decision;

/// <summary>
/// 決め方が例外で失敗したときの代わりの決定。自由時間は core.go_to_next、スマホを開く機会は core.idle（何もしない）にする。
/// </summary>
internal static class DecisionFallback
{
    /// <summary>自由時間の代わりの決定（core.go_to_next）を作る。乱数は引かない。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナの状態。場所にいること。</param>
    /// <param name="now">決定タイミングの時刻。</param>
    public static DeciderOutput FreeTime(RuleContext ctx, SimState state, PersonaState persona, DateTimeOffset now)
    {
        var here = UtilityDecider.DecisionPlace(persona);
        var next = persona.Plan?.NextFixedAfter(now);
        var deadline = next?.Start ?? persona.Plan?.HorizonEnd ?? now;
        var target = GoToNextTarget.Of(ctx.Routes.For(state.Worlds[persona.World].DefId), here, next, now, deadline);
        var payload = new ActivityChosenPayload(ActionRules.GoToNextId, target.Place, target.Route, target.Minutes, target.Wait, false, null);
        var guard = new Guard(
            [
                new ActivityVersionCheck(persona.ActivityVersion),
                new FeasibleCheck(ActionRules.GoToNextId, target.Place),
            ],
            RejectAction.Redecide);
        return new DeciderOutput(payload, guard);
    }

    /// <summary>スマホを開く機会の代わりの決定（何もしない。所要時間 0 の副行動）を作る。</summary>
    /// <param name="persona">ペルソナの状態。場所にいること。</param>
    public static DeciderOutput Phone(PersonaState persona)
    {
        var payload = new ActivityChosenPayload(UtilityDecider.Idle, UtilityDecider.DecisionPlace(persona), [], 0, TimeSpan.Zero, true, null);
        var guard = new Guard([new ActivityVersionCheck(persona.ActivityVersion)], RejectAction.Redecide);
        return new DeciderOutput(payload, guard);
    }
}
