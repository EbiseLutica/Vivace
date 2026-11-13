using System.Collections.Immutable;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Storage;

/// <summary>
/// エンジンの1回のコミット。記録の追加と、適用の後の状態を、1つのまとまりで渡す。
/// </summary>
/// <param name="Events">今回の記録。</param>
/// <param name="State">コミットの後の状態。</param>
public sealed record StorageBatch(ImmutableArray<LoggedEvent> Events, SimState State);
