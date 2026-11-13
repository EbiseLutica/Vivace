namespace Vivace.Core.Domain.State;

/// <summary>ワールドインスタンスの状態。</summary>
public enum WorldStatus
{
    /// <summary>動いている。</summary>
    Alive,

    /// <summary>破棄された（記録として残る）。</summary>
    Destroyed,
}
