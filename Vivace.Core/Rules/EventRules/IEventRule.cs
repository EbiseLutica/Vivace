using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules.EventRules;

/// <summary>イベントタイプごとのルール。guard は評価してから渡される。</summary>
internal interface IEventRule
{
    /// <summary>扱うイベントタイプ。</summary>
    string EventType { get; }

    /// <summary>イベントを適用し、新しい状態と後続のイベントとシグナルを返す。</summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev);
}
