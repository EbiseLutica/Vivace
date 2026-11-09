using Vivace.Core.Definitions;
using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>
/// 状態変数の読み取りと率の切り替え（D5 5.6）。値は「基準の時刻と値」と「いまの率」から、値 + 率 × 経過時間で読む。
/// 率が変わるのは、行動の開始と終了、移動の開始と到着、居合わせの変化のときだけ。Apply の計算は四則演算と比較だけで書く（決定性）。
/// </summary>
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

    /// <summary>範囲に切り詰める。</summary>
    /// <param name="value">値。</param>
    /// <param name="range">範囲。</param>
    public static double Clamp(double value, (double Min, double Max) range) =>
        value < range.Min ? range.Min : value > range.Max ? range.Max : value;

    /// <summary>いまの値を読む。範囲の中に切り詰める。</summary>
    /// <param name="value">状態変数の1本。</param>
    /// <param name="now">読む時刻。</param>
    /// <param name="range">範囲。</param>
    public static double Read(VitalTrack value, DateTimeOffset now, (double Min, double Max) range) =>
        Clamp(value.BaseValue + value.Rate * (now - value.BaseTime).TotalHours, range);

    /// <summary>状態変数の1本を取り出す。</summary>
    /// <param name="vitals">状態変数の集まり。</param>
    /// <param name="vital">取り出す変数。</param>
    public static VitalTrack Get(Vitals vitals, VitalName vital) => vital switch
    {
        VitalName.Energy => vitals.Energy,
        VitalName.Health => vitals.Health,
        VitalName.Boredom => vitals.Boredom,
        _ => vitals.Social,
    };

    /// <summary>状態変数の1本を差し替えた集まりを返す。版は変えない。</summary>
    /// <param name="vitals">状態変数の集まり。</param>
    /// <param name="vital">差し替える変数。</param>
    /// <param name="value">新しい値。</param>
    public static Vitals Put(Vitals vitals, VitalName vital, VitalTrack value) => vital switch
    {
        VitalName.Energy => vitals with { Energy = value },
        VitalName.Health => vitals with { Health = value },
        VitalName.Boredom => vitals with { Boredom = value },
        _ => vitals with { Social = value },
    };

    /// <summary>
    /// ペルソナのいまの居場所に、ほかの人がいるか。移動中の人は、どの場所にもいないものとして扱う（D5 5.10）。
    /// </summary>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナ。</param>
    public static bool IsTogether(SimState state, PersonaState persona)
    {
        if (persona.Location is not AtPlace at)
        {
            return false;
        }

        return state.Personas.Values.Any(other =>
            other.Id != persona.Id
            && other.Status == PersonaStatus.Alive
            && other.Location is AtPlace otherAt
            && otherAt.Place == at.Place);
    }

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
            _ => IsTogether(state, persona) ? personaDef.Vitals.SocialRateTogether : personaDef.Vitals.SocialRateAlone,
        };
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
            var old = Get(vitals, vital);
            var rate = EffectiveRate(state, persona, vital, defs);
            if (rate == old.Rate)
            {
                continue;
            }

            vitals = Put(vitals, vital, new VitalTrack(now, Read(old, now, range), rate));
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
        var old = Get(persona.Vitals, vital);
        var current = Read(old, now, range);
        var value = set ?? current + (delta ?? 0.0);
        var vitals = Put(persona.Vitals, vital, new VitalTrack(now, Clamp(value, range), old.Rate));
        return persona with { Vitals = vitals with { Version = vitals.Version + 1 } };
    }

    /// <summary>
    /// 状態変数の値の検査（D7 7.4）。基準の値が範囲の中にあるかを見る。
    /// </summary>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="defs">定義。</param>
    public static bool InRange(PersonaState persona, Definitions.Definitions defs)
    {
        var def = defs.Personas[persona.DefId].Vitals;
        return AllVitals.All(vital =>
        {
            var (min, max) = Range(vital, def);
            var value = Get(persona.Vitals, vital).BaseValue;
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
