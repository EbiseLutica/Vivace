using Vivace.Core.Decision;

namespace Vivace.Tests.Support;

/// <summary>必ず例外を投げる Decider。決める側の仕事の失敗を起こす。</summary>
internal sealed class ThrowingDecider : IDecider
{
    /// <summary>例外の文言。</summary>
    public const string Message = "わざと失敗させた決定";

    /// <summary>例外を投げる。</summary>
    public DeciderOutput Decide(DecisionContext context) => throw new InvalidOperationException(Message);
}
