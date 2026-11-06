using Vivace.Core.Domain;
using Vivace.Core.Engine;

namespace Vivace.Tests.Engine;

/// <summary>
/// 取り消し（CancelHandler）は、決める側のレーンだけを止める（J-145）。段階2 は効果のハンドラがなく、決めどきは並列に止まらないので、
/// 経路を通すテストは作れない。そこで、取り消しの対象を選ぶ規則を、単体で確かめる。
/// </summary>
public class CancelRuleTests
{
    /// <summary>決める側のレーンは取り消せる。Planner のレーンと、ほかの鍵は取り消せない。</summary>
    [Fact]
    public void OnlyDecisionLanesAreCancellable()
    {
        Assert.True(SimEngine.IsCancellable("decide:kaori@testville"));
        Assert.False(SimEngine.IsCancellable("plan:kaori@testville"));
        Assert.False(SimEngine.IsCancellable("world:testville"));
    }
}
