namespace Vivace.Core.Decision;

/// <summary>自由時間に何をするかを決める口。同じ文脈なら、同じ結果を返すこと。</summary>
public interface IDecider
{
    /// <summary>文脈から次の行動を1つ決める。</summary>
    /// <param name="context">決めどきの文脈。</param>
    DeciderOutput Decide(DecisionContext context);
}
