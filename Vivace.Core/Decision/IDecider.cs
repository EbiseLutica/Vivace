namespace Vivace.Core.Decision;

/// <summary>自由時間に何をするかを決めるインターフェース。同じコンテキストなら、同じ結果を返すこと。</summary>
public interface IDecider
{
    /// <summary>コンテキストから次の行動を1つ決める。</summary>
    /// <param name="context">決定タイミングのコンテキスト。</param>
    DeciderOutput Decide(DecisionContext context);
}
