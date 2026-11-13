using System.Collections.Immutable;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Definitions.Model;

/// <summary>行動の効果。</summary>
/// <param name="Rates">行動の間の率（置き換え）。</param>
/// <param name="OnStart">開始時の増減。</param>
/// <param name="OnEnd">終了時の増減。</param>
/// <param name="Restore">終了時の設定値。</param>
/// <param name="RepeatDecay">同じ行動が続いたときの退屈度の減り方。</param>
/// <param name="Sleep">睡眠としての扱い。</param>
/// <param name="Handler">起こすハンドラ（"sns.inbox" など）。</param>
public sealed record ActionEffects(
    ImmutableDictionary<VitalName, double> Rates,
    ImmutableDictionary<VitalName, double> OnStart,
    ImmutableDictionary<VitalName, double> OnEnd,
    ImmutableDictionary<VitalName, double> Restore,
    double? RepeatDecay,
    SleepEffect? Sleep,
    string? Handler);
