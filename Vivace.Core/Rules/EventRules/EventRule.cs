using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules.EventRules;

/// <summary>ペイロードを型つきで受け取るルールの基底。</summary>
/// <typeparam name="TPayload">扱うイベントのペイロードの型。</typeparam>
internal abstract class EventRule<TPayload> : IEventRule
    where TPayload : EventPayload
{
    /// <summary>扱うイベントタイプ。</summary>
    public abstract string EventType { get; }

    /// <summary>ペイロードを取り出して、型つきの Apply に渡す。</summary>
    public ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev) => Apply(ctx, state, ev, (TPayload)ev.Payload);

    /// <summary>ペイロードを型つきで受け取り、イベントを適用する。</summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <param name="payload">イベントのペイロード。</param>
    protected abstract ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, TPayload payload);
}
