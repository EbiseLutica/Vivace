using System.Globalization;
using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>計画の期間の鍵と、Planner を期間ごとに一度だけ起こす手順。</summary>
internal static class PlannerHelper
{
    /// <summary>期間の鍵（時刻）。記憶の整理を起こすブロックのない期間で使う。</summary>
    /// <param name="time">時刻。</param>
    internal static string PeriodKeyAt(DateTimeOffset time) =>
        "time:" + time.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    /// <summary>
    /// 期間 periodKey の Planner を起こす。同じ期間で動いていれば null。動いていなければ、期間の印を付けたペルソナと、Planner の起動を返す。
    /// </summary>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="periodKey">期間の鍵。</param>
    /// <param name="seq">契機のイベントの連番（シグナルの順序に使う）。</param>
    /// <param name="now">契機の時刻。</param>
    internal static (PersonaState Persona, StartPlanner Signal)? Wake(PersonaState persona, string periodKey, long seq, DateTimeOffset now)
    {
        if (persona.PlannedFor == periodKey)
        {
            return null;
        }

        return (persona with { PlannedFor = periodKey }, new StartPlanner(seq, persona.Id, now));
    }
}
