using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>選んだ行動と場所が、いまの状態でも実行できること。</summary>
/// <param name="ActionId">行動の定義 ID。</param>
/// <param name="Place">場所の ID。</param>
public sealed record FeasibleCheck(string ActionId, PlaceId Place) : GuardCheck;
