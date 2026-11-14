namespace Vivace.Core.Events.Guards;

/// <summary>行動のバージョンが、決めたときと同じであること。</summary>
/// <param name="Expected">決めたときの行動のバージョン。</param>
public sealed record ActivityVersionCheck(long Expected) : GuardCheck;
