using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;

namespace Vivace.Core.Storage;

/// <summary>
/// エンジンの1回のコミット。新しく積んだイベント（未適用の行の挿入）、既存の行の状態の変化、コミットの後の状態を、1つのまとまりで渡す。
/// </summary>
/// <param name="Enqueued">今回積んだイベント（未適用の行として挿入する）。</param>
/// <param name="Updates">既存の行の状態の変化（適用、処理中、捨て、却下、処理中から適用へ）。</param>
/// <param name="State">コミットの後の状態。</param>
public sealed record StorageBatch(ImmutableArray<SimEvent> Enqueued, ImmutableArray<EventRowUpdate> Updates, SimState State);
