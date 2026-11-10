using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Schedule;

/// <summary>
/// 1日の固定予定のブロック（現地の日付と、0:00 からの分）。
/// 日をまたぐブロックは、開始した日のものとして、EndMinute が 1440 を超える。
/// </summary>
/// <param name="Id">ブロックの ID（定義 ID@開始日。分割したものは .2 などの番号つき）。</param>
/// <param name="DefId">ブロックの定義 ID。</param>
/// <param name="Date">開始の日付。</param>
/// <param name="StartMinute">開始（0:00 からの分）。</param>
/// <param name="EndMinute">終了（0:00 からの分）。1440 を超えれば翌日。</param>
/// <param name="Action">行動の定義 ID（親から引き継いだものを含む）。</param>
/// <param name="Place">場所の ID。指定がなければ null。</param>
/// <param name="Phone">スマホを開けるか（ブロック、親、行動の順に決める）。</param>
/// <param name="PhoneEvery">長いブロックの途中の機会の間隔。</param>
/// <param name="With">同行するペルソナの定義 ID。</param>
/// <param name="OnPartnerAbsent">同行者が抜けたときの扱い。</param>
public sealed record ResolvedBlock(
    string Id,
    string DefId,
    DateOnly Date,
    int StartMinute,
    int EndMinute,
    string Action,
    string? Place,
    PhonePolicy Phone,
    TimeSpan? PhoneEvery,
    ImmutableArray<string> With,
    PartnerAbsentPolicy OnPartnerAbsent);
