using Vivace.Core.Time;

namespace Vivace.Core.Definitions;

/// <summary>期間（毎年繰り返す）。</summary>
/// <param name="Id">期間の ID。</param>
/// <param name="From">開始の月日。</param>
/// <param name="To">終了の月日。From より小さければ年をまたぐ。</param>
/// <param name="Source">出どころ。</param>
public sealed record PeriodDef(string Id, MonthDay From, MonthDay To, DefinitionSource Source);
