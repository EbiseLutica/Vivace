using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Definitions.Model;

/// <summary>固定予定のブロック。</summary>
/// <param name="Id">ブロックの定義 ID。</param>
/// <param name="Within">親のブロックの ID（入れ子のとき）。</param>
/// <param name="In">所属する期間の ID。空なら、すべての期間で使う。</param>
/// <param name="Days">曜日。空なら、毎日。</param>
/// <param name="Ranges">時間の範囲（from と to、または times）。</param>
/// <param name="Action">行動の定義 ID。親から引き継ぐことがある。</param>
/// <param name="Place">場所の ID。</param>
/// <param name="SkipHoliday">祝日に休むか。</param>
/// <param name="Phone">スマホを開けるか。</param>
/// <param name="With">同行するペルソナの定義 ID。</param>
/// <param name="OnPartnerAbsent">同行者が抜けたときの扱い。</param>
/// <param name="PhoneEvery">長いブロックの途中で、スマホを開く機会を出す間隔。書いたときだけ。</param>
/// <param name="Source">定義元。</param>
public sealed record BlockDef(
    string Id,
    string? Within,
    ImmutableArray<string> In,
    ImmutableArray<DayOfWeek> Days,
    ImmutableArray<TimeRange> Ranges,
    string? Action,
    string? Place,
    bool? SkipHoliday,
    PhonePolicy? Phone,
    ImmutableArray<string> With,
    PartnerAbsentPolicy? OnPartnerAbsent,
    TimeSpan? PhoneEvery,
    DefinitionSource Source);
