using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>イベントタイプごとのルール。guard は評価してから渡される。</summary>
internal interface IEventRule
{
    /// <summary>扱うイベントタイプ。</summary>
    string EventType { get; }

    /// <summary>イベントを適用し、新しい状態と後続のイベントとシグナルを返す。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev);

    /// <summary>投入 API から入るイベントを検査する。通らなければ拒否の理由を返す。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">いまの状態。</param>
    /// <param name="pending">未適用のイベント。</param>
    /// <param name="payload">投入するイベントの中身。</param>
    /// <param name="at">投入するイベントの時刻。</param>
    string? CheckInjection(RuleContext ctx, SimState state, IEnumerable<SimEvent> pending, EventPayload payload, DateTimeOffset at);
}
