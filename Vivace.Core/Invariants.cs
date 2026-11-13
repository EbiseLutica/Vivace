using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core;

/// <summary>適用のたびに検査する不変条件。時刻が戻らないこと、状態変数が範囲の中にあること、各ペルソナに次の自分のイベントがあること、未適用のイベントが現在位置より前にないこと、時刻が進んでいること。</summary>
public static class Invariants
{
    /// <summary>1つの時刻に、この数を超えて適用が続いたら、時刻が進まないとして止める。</summary>
    public const int MaxAppliedAtOneInstant = 10_000;

    /// <summary>1回の適用の後に、状態を検査する。</summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">適用の後の状態。</param>
    /// <param name="previousTime">適用の前の位置の時刻。</param>
    /// <param name="busy">決める側の仕事が動いている（走っている、または出力を待っている）ペルソナ。</param>
    /// <param name="pending">未適用のイベントを、ペルソナごとに引けるもの。</param>
    /// <param name="earliestPending">未適用のイベントの最小の時刻（なければ null）。</param>
    /// <exception cref="InvariantViolationException">いずれかが破れた。</exception>
    public static void Check(RuleContext ctx, SimState state, DateTimeOffset previousTime, IReadOnlySet<PersonaId> busy, ILookup<PersonaId, SimEvent> pending, DateTimeOffset? earliestPending)
    {
        if (state.Cursor.Time < previousTime)
        {
            throw new InvariantViolationException($"時刻が戻った: {previousTime:o} -> {state.Cursor.Time:o}");
        }

        if (earliestPending is { } earliest && earliest < state.Cursor.Time)
        {
            throw new InvariantViolationException($"未適用のイベントが現在位置より前にある: {earliest:o} < {state.Cursor.Time:o}");
        }

        foreach (var persona in state.Personas.Values.Where(p => p.Status == PersonaStatus.Alive))
        {
            if (!VitalRules.InRange(persona, ctx.Defs))
            {
                throw new InvariantViolationException($"状態変数が範囲の外: {persona.Id}");
            }

            if (busy.Contains(persona.Id))
            {
                continue;
            }

            var events = pending[persona.Id].ToList();
            if (persona.Deciding && !events.Any(ev => ev.EventType == EventTypes.ActivityChosen))
            {
                throw new InvariantViolationException($"決め中なのに、決める仕事が動いていない: {persona.Id}");
            }

            if (persona.Activity is { } activity)
            {
                var ended = events.Any(ev => ev.Payload is ActionEndedPayload end
                    && end.ActionId == activity.ActionId
                    && end.ActivityVersion == persona.ActivityVersion);
                if (!ended)
                {
                    throw new InvariantViolationException($"行動中なのに、同じ版の行動の終わりが予定されていない: {persona.Id}");
                }

                continue;
            }

            if (persona.Location is Traveling && !events.Any(ev => ev.EventType == EventTypes.TravelArrived))
            {
                throw new InvariantViolationException($"移動中なのに、到着が予定されていない: {persona.Id}");
            }

            if (persona.Location is AtPlace && events.Count == 0)
            {
                throw new InvariantViolationException($"次の自分のイベントがない（止まっている）: {persona.Id}");
            }

            if (persona.Location is AtPlace
                && !persona.Deciding
                && persona.Plan?.FreeAt(state.Cursor.Time) is not null
                && !events.Any(ev => ev.EventType is EventTypes.DecisionRequested
                    or EventTypes.ActivityChosen
                    or EventTypes.ActionStarted
                    or EventTypes.TravelStarted
                    || ev.Payload is BlockStartedPayload { BlockId: null }))
            {
                throw new InvariantViolationException($"自由時間なのに、決めどきが予定されていない（手空き）: {persona.Id}");
            }
        }
    }

    /// <summary>時刻が進まないまま、適用が続いていないかを見る。</summary>
    /// <param name="streak">同じ時刻に続けて適用したイベントの数。</param>
    /// <exception cref="InvariantViolationException">上限を超えた。</exception>
    public static void CheckProgress(int streak)
    {
        if (streak > MaxAppliedAtOneInstant)
        {
            throw new InvariantViolationException($"時刻が進まないまま、{streak} 件を超えて適用が続いた");
        }
    }
}
