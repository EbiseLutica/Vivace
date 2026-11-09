using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>
/// 固定予定と計画のプランの読み取り。
/// </summary>
public static class PlanRules
{
    /// <summary>時刻 t が自由時間の中か（自由時間のブロックの開始以上、終了未満）。</summary>
    /// <param name="plan">プラン。</param>
    /// <param name="t">時刻。</param>
    public static PlanBlock? FreeAt(DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks.FirstOrDefault(block => block.Kind == BlockKind.Free && block.Start <= t && t < block.End);

    /// <summary>時刻 t に、固定・計画のブロックが始まるものがあるか。</summary>
    /// <param name="plan">プラン。</param>
    /// <param name="t">時刻。</param>
    public static bool BlockStartsAt(DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks.Any(block => block.Kind != BlockKind.Free && block.Start == t) ?? false;

    /// <summary>時刻 t より後で最初に始まる固定・計画のブロック。なければ null。</summary>
    /// <param name="plan">プラン。</param>
    /// <param name="t">時刻。</param>
    public static PlanBlock? NextFixedAfter(DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks
            .Where(block => block.Kind != BlockKind.Free && block.Start > t)
            .OrderBy(block => block.Start)
            .ThenBy(block => block.BlockId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>時刻 t を含む、固定・計画のブロック。なければ null。</summary>
    /// <param name="plan">プラン。</param>
    /// <param name="t">時刻。</param>
    public static PlanBlock? BlockCoveringAt(DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks.FirstOrDefault(block => block.Kind != BlockKind.Free && block.Start <= t && t < block.End);

    /// <summary>時刻 t に、いま有効なブロック（ID と時刻の範囲が合うもの）。なければ null。</summary>
    /// <param name="plan">プラン。</param>
    /// <param name="blockId">ブロックの ID。</param>
    /// <param name="t">時刻。</param>
    public static PlanBlock? CurrentBlock(DayPlan? plan, string blockId, DateTimeOffset t) =>
        plan?.Blocks.FirstOrDefault(block => block.BlockId == blockId && block.Start <= t && t < block.End);
}
