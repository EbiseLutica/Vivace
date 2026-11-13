namespace Vivace.Core.Domain.State;

/// <summary>ペルソナインスタンスの状態。</summary>
public enum PersonaStatus
{
    /// <summary>動いている。</summary>
    Alive,

    /// <summary>追いつきの上限を超えた空白の区間に入っている。</summary>
    Gap,

    /// <summary>破棄された（記録として残る）。</summary>
    Destroyed,
}
