using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules;

/// <summary>状態変数の率を、いまの居合わせに合わせて計算し直す。</summary>
internal static class Rebalancing
{
    /// <summary>
    /// 状態変数の率を計算し直す。mover と、places にいる人が対象（居合わせが変わったので、社交欲求の率が変わりうる）。
    /// </summary>
    internal static SimState Rebalance(RuleContext ctx, SimState state, DateTimeOffset now, PersonaId mover, params PlaceId[] places)
    {
        foreach (var id in state.Personas.Keys.OrderBy(key => key.Value, StringComparer.Ordinal).ToList())
        {
            var persona = state.Personas[id];
            if (persona.Status != PersonaStatus.Alive)
            {
                continue;
            }

            var affected = id == mover || (persona.Location is AtPlace at && places.Contains(at.Place));
            if (!affected)
            {
                continue;
            }

            var refreshed = VitalRules.Refresh(state, persona, now, ctx.Defs);
            if (!ReferenceEquals(refreshed, persona))
            {
                state = state.WithPersona(refreshed);
            }
        }

        return state;
    }
}
