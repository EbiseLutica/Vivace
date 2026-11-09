using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>1日のプラン（ホライズンからの範囲）。</summary>
/// <param name="Version">プランの版。古いものを捨てる判定に使う。</param>
/// <param name="HorizonStart">範囲の開始。</param>
/// <param name="HorizonEnd">範囲の終了。</param>
/// <param name="NextRebuildAt">次に作り直す時刻。</param>
/// <param name="Source">由来。</param>
/// <param name="Blocks">ブロックの列。</param>
/// <param name="Summary">期間の一言の要約（文脈に使う）。</param>
public sealed record DayPlan(
    long Version,
    DateTimeOffset HorizonStart,
    DateTimeOffset HorizonEnd,
    DateTimeOffset NextRebuildAt,
    PlanSource Source,
    ImmutableArray<PlanBlock> Blocks,
    string? Summary);
