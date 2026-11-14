using System.Collections.Immutable;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Definitions.Model;

/// <summary>出来事の効果。書いたものだけが入る。</summary>
/// <param name="SlowPlace">区間の移動時間を増やす。</param>
/// <param name="ClosePlace">場所を閉鎖する。</param>
/// <param name="Vitals">状態変数の増減。</param>
/// <param name="Condition">付ける状態の定義 ID。</param>
public sealed record IncidentEffects(
    SlowPlaceEffect? SlowPlace,
    ClosePlaceEffect? ClosePlace,
    ImmutableDictionary<VitalName, double> Vitals,
    string? Condition);
