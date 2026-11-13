using System.Collections.Immutable;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Definitions.Model;

/// <summary>状態（コンディション）の定義。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Display">表示名。</param>
/// <param name="Rates">既定の率に足す率。</param>
/// <param name="Weights">Decider の重みに掛ける係数。キーは行動 ID か "tag:行動タグ"。</param>
/// <param name="Forbid">候補から外す行動 ID か行動タグ。</param>
/// <param name="Thresholds">反射のしきい値のずれ。</param>
/// <param name="Source">出どころ。</param>
public sealed record ConditionDef(
    string Id,
    string Display,
    ImmutableDictionary<VitalName, double> Rates,
    ImmutableDictionary<string, double> Weights,
    ImmutableArray<string> Forbid,
    ImmutableDictionary<VitalName, double> Thresholds,
    DefinitionSource Source);
