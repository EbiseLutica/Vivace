namespace Vivace.Core.Definitions;

/// <summary>フォローバックの方針。</summary>
public enum FollowBackPolicy
{
    /// <summary>フォローされたら必ずフォローし返す。</summary>
    Always,

    /// <summary>フォローし返さない。</summary>
    Never,
}
