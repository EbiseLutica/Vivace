using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>
/// 解決済みの出来事の効果（D2 2.3、2.7、2.8）。場所を決める抽選の結果を含み、定義を引き直さなくても解消できる。
/// 同じ出来事が移動時間と閉鎖の両方を持つことがある。
/// </summary>
/// <param name="SlowPlace">移動時間を増やす効果。</param>
/// <param name="ClosedPlace">閉鎖する場所。</param>
/// <param name="Vitals">状態変数の増減。</param>
/// <param name="Condition">付ける状態の定義 ID。</param>
public sealed record ResolvedIncidentEffects(
    ResolvedSlowPlace? SlowPlace,
    PlaceId? ClosedPlace,
    ImmutableDictionary<VitalName, double> Vitals,
    string? Condition);
