using Vivace.Core.Domain;
using Vivace.Tests.Support;

namespace Vivace.Tests.Domain;

/// <summary>DayPlan の読み取りを、手で作った小さなプランで確かめる。</summary>
public class DayPlanTests
{
    /// <summary>同じ開始の固定ブロックが2つあれば、次の固定ブロックは ID の序数で小さいほう。並びの順には依らない。</summary>
    [Fact]
    public void NextFixedAfterBreaksTiesByBlockIdOrdinal()
    {
        var start = Fixture.Now.AddHours(1);
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [
                new PlanBlock("zeta@2026-11-01", BlockKind.Fixed, start, start.AddHours(1), "read_book", null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
                new PlanBlock("alpha@2026-11-01", BlockKind.Fixed, start, start.AddHours(1), "read_book", null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            ],
            null);

        Assert.Equal("alpha@2026-11-01", plan.NextFixedAfter(Fixture.Now)?.BlockId);
    }
}
