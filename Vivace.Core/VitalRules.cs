using Vivace.Core.Definitions;
using Vivace.Core.Domain;

namespace Vivace.Core;

/// <summary>状態変数の読み取りと率の切り替え。値は、基準の時刻の値と率から、値 + 率 × 経過時間で読む。</summary>
public static class VitalRules
{
    /// <summary>起きている間の気力の率（1時間あたり）。</summary>
    public const double AwakeEnergyRate = -4.0;

    /// <summary>健康度の既定の率（1時間あたり）。ゆっくり戻る。</summary>
    public const double DefaultHealthRate = 0.2;

    /// <summary>退屈度の既定の率（1時間あたり）。</summary>
    public const double DefaultBoredomRate = 3.0;

    /// <summary>状態変数の範囲を返す。社交欲求は定義の下限と上限、ほかは 0 から 100。</summary>
    /// <param name="vital">状態変数。</param>
    /// <param name="def">ペルソナの状態変数の設定。</param>
    public static (double Min, double Max) Range(VitalName vital, VitalsDef def) => vital switch
    {
        VitalName.Social => (def.SocialFloor, def.SocialCeiling),
        _ => (0.0, 100.0),
    };

    /// <summary>
    /// 状態変数のいまの率。行動が率を書けばそれを使い、書かなければ既定の率（社交欲求は居合わせで変わる）。
    /// </summary>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="vital">状態変数。</param>
    /// <param name="defs">定義。</param>
    public static double EffectiveRate(SimState state, PersonaState persona, VitalName vital, Definitions.Definitions defs)
    {
        if (persona.Activity is { } activity
            && defs.Actions.TryGetValue(activity.ActionId, out var action)
            && action.Effects.Rates.TryGetValue(vital, out var rate))
        {
            return rate;
        }

        var personaDef = defs.Personas[persona.DefId];
        return vital switch
        {
            VitalName.Energy => AwakeEnergyRate,
            VitalName.Health => DefaultHealthRate,
            VitalName.Boredom => DefaultBoredomRate,
            _ => state.IsTogether(persona) ? personaDef.Vitals.SocialRateTogether : personaDef.Vitals.SocialRateAlone,
        };
    }

    /// <summary>
    /// 状態変数の率を計算し直す。mover と、places にいる人が対象（居合わせが変わったので、社交欲求の率が変わりうる）。
    /// </summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">全体の状態。</param>
    /// <param name="now">時刻。</param>
    /// <param name="mover">動いたペルソナ。</param>
    /// <param name="places">居合わせが変わった場所。</param>
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

            var refreshed = Refresh(state, persona, now, ctx.Defs);
            if (!ReferenceEquals(refreshed, persona))
            {
                state = state.WithPersona(refreshed);
            }
        }

        return state;
    }

    /// <summary>
    /// ペルソナの状態変数の率を、いまの状態に合わせて計算し直す。率が変わった変数だけ、いまの値を基準にして置き直す。
    /// 率が1つでも変わったら、状態変数の版を上げる。
    /// </summary>
    /// <param name="state">全体の状態（ペルソナの居場所や行動は、呼ぶ前に更新しておく）。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="now">時刻。</param>
    /// <param name="defs">定義。</param>
    public static PersonaState Refresh(SimState state, PersonaState persona, DateTimeOffset now, Definitions.Definitions defs)
    {
        var def = defs.Personas[persona.DefId];
        var vitals = persona.Vitals;
        var changed = false;
        foreach (var vital in AllVitals)
        {
            var range = Range(vital, def.Vitals);
            var old = vitals.Get(vital);
            var rate = EffectiveRate(state, persona, vital, defs);
            if (rate == old.Rate)
            {
                continue;
            }

            vitals = vitals.With(vital, new VitalTrack(now, old.ValueAt(now, range), rate));
            changed = true;
        }

        return changed ? persona with { Vitals = vitals with { Version = vitals.Version + 1 } } : persona;
    }

    /// <summary>
    /// 状態変数の値を、いまの値から増減するか、設定する。範囲に切り詰め、基準の時刻を now に置き直す。
    /// 率は変えない（呼んだあとに Refresh で計算し直す）。
    /// </summary>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="vital">状態変数。</param>
    /// <param name="now">時刻。</param>
    /// <param name="defs">定義。</param>
    /// <param name="delta">増減（設定のときは null）。</param>
    /// <param name="set">設定する値（増減のときは null）。</param>
    public static PersonaState Change(PersonaState persona, VitalName vital, DateTimeOffset now, Definitions.Definitions defs, double? delta, double? set)
    {
        var range = Range(vital, defs.Personas[persona.DefId].Vitals);
        var old = persona.Vitals.Get(vital);
        var current = old.ValueAt(now, range);
        var value = set ?? current + (delta ?? 0.0);
        var vitals = persona.Vitals.With(vital, new VitalTrack(now, VitalTrack.Clamp(value, range), old.Rate));
        return persona with { Vitals = vitals with { Version = vitals.Version + 1 } };
    }

    /// <summary>
    /// 状態変数の値の検査。基準の値が範囲の中にあるかを見る。
    /// </summary>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="defs">定義。</param>
    public static bool InRange(PersonaState persona, Definitions.Definitions defs)
    {
        var def = defs.Personas[persona.DefId].Vitals;
        return AllVitals.All(vital =>
        {
            var (min, max) = Range(vital, def);
            var value = persona.Vitals.Get(vital).BaseValue;
            return value >= min && value <= max;
        });
    }

    /// <summary>4つの状態変数。</summary>
    public static readonly VitalName[] AllVitals =
    [
        VitalName.Boredom,
        VitalName.Energy,
        VitalName.Health,
        VitalName.Social,
    ];
}
