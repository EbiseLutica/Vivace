using System.Collections.Immutable;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Storage;

/// <summary>保存先から起動のために読んだもの。</summary>
/// <param name="State">最後に適用した状態。適用の記録が1件もなければ null。</param>
/// <param name="Open">未適用と処理中の行（連番の昇順でなくてもよい）。</param>
/// <param name="MaxSeq">保存先にある連番の最大（記録がなければ 0）。次の連番はこの次から振る。</param>
public sealed record RecoveredRecords(SimState? State, ImmutableArray<EventRow> Open, long MaxSeq);
