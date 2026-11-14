using Vivace.Core.Definitions.Model;
using Vivace.Core.Routes;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>
/// 規則のコンテキスト。定義と、ワールドごとの最短経路のテーブル。どちらも定義が変わるまで不変。
/// </summary>
/// <param name="Defs">読み込んだ定義。</param>
/// <param name="Routes">ワールドごとの最短経路のテーブル。</param>
/// <param name="Holidays">祝日のカレンダー。省略すると祝日のないカレンダー。</param>
public sealed record RuleContext(DefinitionPack Defs, RouteCache Routes, IHolidayCalendar Holidays)
{
    /// <summary>祝日のカレンダーを省略したコンテキスト。</summary>
    public RuleContext(DefinitionPack defs, RouteCache routes) : this(defs, routes, NoHolidays.Instance)
    {
    }
}
