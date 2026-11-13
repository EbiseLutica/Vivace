namespace Vivace.Core.Definitions.Model;

/// <summary>行動の好みの種類。</summary>
public enum PreferenceKind
{
    /// <summary>好き（"like"）。</summary>
    Like,

    /// <summary>嫌い（"dislike"）。</summary>
    Dislike,

    /// <summary>しない（"forbid"。数値の 0 もこれ）。</summary>
    Forbid,

    /// <summary>数値の倍率。</summary>
    Factor,
}
