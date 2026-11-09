using System.Collections.Immutable;

namespace Vivace.Core.Events;

/// <summary>
/// 適用してよい条件（guard）。決める側の出力に付け、Apply が直前に評価する（D1 1.7）。
/// </summary>
/// <param name="Checks">確かめることの列。すべて通ったときだけ適用する。</param>
/// <param name="OnReject">却下されたときの動き。</param>
public sealed record Guard(ImmutableArray<GuardCheck> Checks, RejectAction OnReject);
