using Vivace.Core.Routes;
using Vivace.Core.Time;

namespace Vivace.Core.Rules;

/// <summary>
/// 規則の文脈。定義と、ワールドごとの最短経路の表。どちらも定義が変わるまで不変。
/// </summary>
/// <param name="Defs">読み込んだ定義。</param>
/// <param name="Routes">ワールドごとの最短経路の表。</param>
/// <param name="Holidays">祝日の暦。省略すると祝日のない暦。</param>
public sealed record RuleContext(Definitions.Definitions Defs, RouteCache Routes, IHolidayCalendar Holidays)
{
    /// <summary>祝日の暦を省略した文脈。</summary>
    public RuleContext(Definitions.Definitions defs, RouteCache routes) : this(defs, routes, NoHolidays.Instance)
    {
    }
}
