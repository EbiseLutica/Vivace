using Vivace.Core.Definitions.Model;


namespace Vivace.Core.Schedule;

/// <summary>固定予定の問題。どのブロックの定義で起きたかを持つ。</summary>
/// <param name="Block">問題のブロックの定義。</param>
/// <param name="Message">説明。</param>
public sealed record ScheduleIssue(BlockDef Block, string Message);
