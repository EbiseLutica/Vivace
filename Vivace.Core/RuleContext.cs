using Vivace.Core.Definitions.Model;
using Vivace.Core.Routes;
using Vivace.Core.Time;

namespace Vivace.Core;

/// <summary>
/// 規則の文脈。定義と、ワールドごとの最短経路の表。どちらも定義が変わるまで不変。
/// </summary>
/// <param name="Defs">読み込んだ定義。</param>
/// <param name="Routes">ワールドごとの最短経路の表。</param>
/// <param name="Holidays">祝日のカレンダー。省略すると祝日のないカレンダー。</param>
public sealed record RuleContext(DefinitionPack Defs, RouteCache Routes, IHolidayCalendar Holidays)
{
    /// <summary>祝日のカレンダーを省略した文脈。</summary>
    public RuleContext(DefinitionPack defs, RouteCache routes) : this(defs, routes, NoHolidays.Instance)
    {
    }
}
