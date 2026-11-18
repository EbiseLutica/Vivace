using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>投稿のスキップを当てる。最後のスキップの時刻を更新する（スキップのクールダウンの基準）。</summary>
internal sealed class PostSkippedRule : EventRule<PostSkippedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PostSkipped;

    /// <summary>投稿のスキップを当てる。最後のスキップの時刻を更新する。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, PostSkippedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        return ApplyResult.Applied(state.WithPersona(persona with { Posting = persona.Posting.Skipped(ev.Time) }));
    }
}
