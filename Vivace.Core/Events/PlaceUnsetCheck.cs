namespace Vivace.Core.Events;

/// <summary>同じ名前の場所が、まだないこと。</summary>
/// <param name="Name">場所の名前。</param>
public sealed record PlaceUnsetCheck(string Name) : GuardCheck;
