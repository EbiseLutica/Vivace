namespace Vivace.Core.Events.Guards;

/// <summary>行動の版が、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときの行動の版。</param>
public sealed record ActivityVersionCheck(long Expected) : GuardCheck;
