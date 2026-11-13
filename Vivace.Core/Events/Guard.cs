using System.Collections.Immutable;
using Vivace.Core.Events.Guards;

namespace Vivace.Core.Events;

/// <summary>
/// 適用してよい条件（guard）。決める側の出力に付け、Apply が直前に評価する。
/// </summary>
/// <param name="Checks">確かめることの列。すべて通ったときだけ適用する。</param>
/// <param name="OnReject">却下されたときの動き。</param>
public sealed record Guard(ImmutableArray<GuardCheck> Checks, RejectAction OnReject);
