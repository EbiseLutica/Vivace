namespace Vivace.Core.Decision;

/// <summary>
/// 自由時間の決定（D4 4.2）。同期の純粋な関数で、LLM は使わない。差し替えて試せるように口にする（VISION 5章）。
/// </summary>
public interface IDecider
{
    /// <summary>決める。出力は activity.chosen の1件（P-12）。</summary>
    /// <param name="context">決めどきの文脈。</param>
    DeciderOutput Decide(DecisionContext context);
}
