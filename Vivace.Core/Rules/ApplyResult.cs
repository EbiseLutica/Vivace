using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Rules;

/// <summary>イベントの適用の結果。</summary>
/// <param name="State">新しい状態。</param>
/// <param name="Outcome">結果。</param>
/// <param name="Reason">捨てた、または却下した理由（適用のときは null）。</param>
/// <param name="FollowUps">後続のイベント。</param>
/// <param name="Signals">エンジンへの依頼。</param>
public sealed record ApplyResult(
    SimState State,
    EventOutcome Outcome,
    string? Reason,
    ImmutableArray<NewEvent> FollowUps,
    ImmutableArray<EngineSignal> Signals)
{
    /// <summary>適用した結果を作る。</summary>
    public static ApplyResult Applied(SimState state, ImmutableArray<NewEvent> followUps = default, ImmutableArray<EngineSignal> signals = default) =>
        new(state, EventOutcome.Applied, null, followUps.IsDefault ? ImmutableArray<NewEvent>.Empty : followUps, signals.IsDefault ? ImmutableArray<EngineSignal>.Empty : signals);

    /// <summary>捨てた結果を作る。状態は変えない。</summary>
    public static ApplyResult Skipped(SimState state, string reason) => new(state, EventOutcome.Skipped, reason, [], []);
}
