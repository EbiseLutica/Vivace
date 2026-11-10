using Vivace.Core.Domain;
using Vivace.Core.Events;
using static Vivace.Core.Rules.Guards;

namespace Vivace.Core.Rules;

/// <summary>イベントを適用し、新しい状態と後続のイベントとシグナルを返す。同じ入力なら同じ結果になる。</summary>
public static class Rules
{
    /// <summary>1つのイベントを適用する。guard を先に評価し、通らなければ却下する。</summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <exception cref="NotSupportedException">扱わないイベントタイプ。</exception>
    public static ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (ev.Guard is { } guard && CheckGuard(ctx, state, ev, guard) is { } failure)
        {
            return Rejected(state, ev, guard, failure);
        }

        if (!RuleTable.Entries.TryGetValue(ev.EventType, out var rule))
        {
            throw new NotSupportedException($"段階2 では扱わないイベントタイプです: {ev.EventType}");
        }

        return rule.Apply(ctx, state, ev);
    }
}
