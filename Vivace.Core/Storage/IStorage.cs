using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Storage;

/// <summary>イベントの適用の結果の記録（D3 3.8 の event 表の1行に当たる）。</summary>
public enum EventStatus
{
    /// <summary>適用した。</summary>
    Applied,

    /// <summary>版が古いなどで捨てた。</summary>
    Skipped,

    /// <summary>guard が通らず却下した。</summary>
    Rejected,
}

/// <summary>適用の記録の1件。行は消さず、中身も書き換えない（D1 1.8）。</summary>
/// <param name="Event">イベント。</param>
/// <param name="Status">結果。</param>
/// <param name="ApplyNo">適用番号（適用したときだけ）。</param>
/// <param name="Reason">捨てた、または却下した理由。</param>
public sealed record LoggedEvent(SimEvent Event, EventStatus Status, long? ApplyNo, string? Reason);

/// <summary>
/// エンジンの1回のコミット（D1 1.9）。記録の追加と、適用の後の状態を、1つのまとまりで渡す。
/// </summary>
/// <param name="Events">今回の記録。</param>
/// <param name="State">コミットの後の状態。</param>
public sealed record StorageBatch(ImmutableArray<LoggedEvent> Events, SimState State);

/// <summary>
/// 状態とイベントの保存の口（D4 4.2）。段階2 では最小の形（書き込みだけ）。読み込みと記憶は段階4 と段階7 で足す。
/// </summary>
public interface IStorage : IAsyncDisposable
{
    /// <summary>1つのトランザクションとして書く。</summary>
    /// <param name="batch">書くもの。</param>
    /// <param name="ct">キャンセル。</param>
    Task CommitAsync(StorageBatch batch, CancellationToken ct);
}
