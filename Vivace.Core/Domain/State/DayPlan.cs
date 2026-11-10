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
    string? Summary)
{
    /// <summary>時刻 t が自由時間の中か（自由時間のブロックの開始以上、終了未満）。中なら、そのブロックを返す。</summary>
    /// <param name="t">時刻。</param>
    public PlanBlock? FreeAt(DateTimeOffset t) =>
        Blocks.FirstOrDefault(block => block.Kind == BlockKind.Free && block.Start <= t && t < block.End);

    /// <summary>ブロックが、プランに同じ種類・ID・時刻で入っているか。</summary>
    /// <param name="block">ブロック。</param>
    public bool Contains(PlanBlock block) =>
        Blocks.Any(old => old.Kind == block.Kind && old.BlockId == block.BlockId && old.Start == block.Start && old.End == block.End);

    /// <summary>時刻 t に、固定・計画のブロックが始まるものがあるか。</summary>
    /// <param name="t">時刻。</param>
    public bool BlockStartsAt(DateTimeOffset t) =>
        Blocks.Any(block => block.Kind != BlockKind.Free && block.Start == t);

    /// <summary>時刻 t より後で最初に始まる固定・計画のブロック。なければ null。</summary>
    /// <param name="t">時刻。</param>
    public PlanBlock? NextFixedAfter(DateTimeOffset t) =>
        Blocks
            .Where(block => block.Kind != BlockKind.Free && block.Start > t)
            .OrderBy(block => block.Start)
            .ThenBy(block => block.BlockId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>時刻 t を含む、固定・計画のブロック。なければ null。</summary>
    /// <param name="t">時刻。</param>
    public PlanBlock? BlockCoveringAt(DateTimeOffset t) =>
        Blocks.FirstOrDefault(block => block.Kind != BlockKind.Free && block.Start <= t && t < block.End);

    /// <summary>時刻 t に、いま有効なブロック（ID と時刻の範囲が合うもの）。なければ null。</summary>
    /// <param name="blockId">ブロックの ID。</param>
    /// <param name="t">時刻。</param>
    public PlanBlock? CurrentBlock(string blockId, DateTimeOffset t) =>
        Blocks.FirstOrDefault(block => block.BlockId == blockId && block.Start <= t && t < block.End);
}
