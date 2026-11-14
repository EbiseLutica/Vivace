using Vivace.Core;

namespace Vivace.Tests.Engine;

/// <summary>取り消しの対象を選ぶ規則を確かめる。決める側のレーンだけを止める。</summary>
public class CancelRuleTests
{
    /// <summary>決める側のレーンは取り消せる。Planner のレーンと、ほかのキーは取り消せない。</summary>
    [Fact]
    public void OnlyDecisionLanesAreCancellable()
    {
        Assert.True(SimEngine.IsCancellable("decide:kaori"));
        Assert.False(SimEngine.IsCancellable("plan:kaori"));
        Assert.False(SimEngine.IsCancellable("world:testville"));
    }
}
