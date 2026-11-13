using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Planning;
using Vivace.Tests.Support;

namespace Vivace.Tests.Planning;

/// <summary>固定予定だけのプランを、テスト用の町の香織の予定（睡眠 23:45〜06:30 JST）で確かめる。</summary>
public class FallbackPlannerTests
{
    /// <summary>23:00 から作ると、いまから 23:45 の睡眠までは自由時間、睡眠が次の作り直しの時刻、範囲はそこから 3 時間後まで。</summary>
    [Fact]
    public void PlanFromTwentyThreeOClockFillsGapsUntilNextSleep()
    {
        var persona = Fixture.WithKaori().Personas[Fixture.Kaori];

        var plan = FallbackPlanner.Plan(Fixture.Context.Defs, persona, Fixture.Now, 1);

        Assert.Equal(PlanSource.Fallback, plan.Source);
        Assert.Equal(Fixture.Now.AddMinutes(45), plan.NextRebuildAt);
        Assert.Equal(Fixture.Now.AddMinutes(45 + 180), plan.HorizonEnd);
        Assert.Equal(2, plan.Blocks.Length);
        Assert.Equal(new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddMinutes(45), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue), plan.Blocks[0]);
        Assert.Equal("sleep_weekday@2026-11-01", plan.Blocks[1].BlockId);
        Assert.Equal(BlockKind.Fixed, plan.Blocks[1].Kind);
        Assert.Equal(Fixture.Now.AddMinutes(45), plan.Blocks[1].Start);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 21, 30, 0, TimeSpan.Zero), plan.Blocks[1].End);
        Assert.Equal("core.sleep", plan.Blocks[1].ActionId);
        Assert.Equal(new PlaceId("kaori_room"), plan.Blocks[1].Place);
    }
}
