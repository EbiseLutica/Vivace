namespace Vivace.Core.Definitions.Model;

/// <summary>出来事の定義。</summary>
/// <param name="Id">定義 ID。</param>
/// <param name="Display">表示名。</param>
/// <param name="Target">対象。</param>
/// <param name="Chance">1日あたりの発生確率（0 から 1）。定義ごとに必ず書く。</param>
/// <param name="Hours">発生する時刻の範囲。</param>
/// <param name="Duration">持続時間。</param>
/// <param name="Cooldown">再発までの間隔。</param>
/// <param name="Effects">効果。</param>
/// <param name="Source">定義元。</param>
public sealed record IncidentDef(
    string Id,
    string Display,
    IncidentTarget Target,
    double Chance,
    TimeRange? Hours,
    ActionDuration? Duration,
    TimeSpan? Cooldown,
    IncidentEffects Effects,
    DefinitionSource Source);
