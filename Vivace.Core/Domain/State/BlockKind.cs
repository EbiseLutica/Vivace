namespace Vivace.Core.Domain;

/// <summary>プランのブロックの種類。</summary>
public enum BlockKind
{
    /// <summary>固定予定のブロック。</summary>
    Fixed,

    /// <summary>Planner が埋めた計画のブロック。</summary>
    Planned,

    /// <summary>自由時間。Decider が決める。</summary>
    Free,
}
