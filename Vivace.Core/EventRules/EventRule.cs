using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

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
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <param name="payload">イベントのペイロード。</param>
    protected abstract ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, TPayload payload);

    /// <summary>ペイロードの型を確かめてから、型つきの投入の検査に渡す。</summary>
    public string? CheckInjection(RuleContext ctx, SimState state, IEnumerable<SimEvent> pending, EventPayload payload, DateTimeOffset at) =>
        payload is TPayload typed ? CheckInjection(ctx, state, pending, typed, at) : "中身の型が違う";

    /// <summary>投入 API から入るイベントを、ペイロードを型つきで受け取って検査する。既定は投入を受け付けない。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">いまの状態。</param>
    /// <param name="pending">未適用のイベント。</param>
    /// <param name="payload">投入するイベントの中身。</param>
    /// <param name="at">投入するイベントの時刻。</param>
    /// <returns>拒否の理由。通れば null。</returns>
    protected virtual string? CheckInjection(RuleContext ctx, SimState state, IEnumerable<SimEvent> pending, TPayload payload, DateTimeOffset at) =>
        InjectionValidator.UnsupportedReason;
}
