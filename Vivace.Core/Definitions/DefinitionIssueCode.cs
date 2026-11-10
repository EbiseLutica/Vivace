namespace Vivace.Core.Definitions;

/// <summary>問題の種類。</summary>
public enum DefinitionIssueCode
{
    /// <summary>TOML の構文。</summary>
    SyntaxError,

    /// <summary>同じ表に、同じキーが2回ある。</summary>
    DuplicateKey,

    /// <summary>必須の項目がない。</summary>
    MissingRequired,

    /// <summary>同じ ID が2つある。</summary>
    DuplicateId,

    /// <summary>名前空間の違反（ユーザーの定義に "." がある、組み込みに core. がない など）。</summary>
    Namespace,

    /// <summary>参照先がない。</summary>
    MissingReference,

    /// <summary>両側に書いた辺の時間が食い違う。</summary>
    EdgeMismatch,

    /// <summary>固定予定のブロックが重なる、または親の時間からはみ出す。</summary>
    ScheduleOverlap,

    /// <summary>固定予定の隙間が、場所の間の移動時間より短い（警告）。</summary>
    ScheduleGap,

    /// <summary>反射の行動の所要時間が範囲になっている。</summary>
    ReflexRange,

    /// <summary>core.go_to_next を forbid している。</summary>
    ForbidGoToNext,

    /// <summary>値の型と範囲、形式が正しくない。</summary>
    InvalidValue,

    /// <summary>未知のキー（警告）。</summary>
    UnknownKey,

    /// <summary>どの行動の requires にも出てこない場所のタグ（警告）。</summary>
    UnusedPlaceTag,

    /// <summary>requires のタグを、ペルソナのワールドのどの場所も持たない（警告）。</summary>
    UnsatisfiableRequirement,

    /// <summary>同行を片側にしか書いていない（警告）。</summary>
    OneSidedWith,

    /// <summary>使う行動のワイルドカード（misskey.* など）に、当たる行動がない（警告。プラグインなしでも読める）。</summary>
    UnmatchedWildcard,

    /// <summary>同行の相手と、時刻、場所、行動が一致しない（警告）。</summary>
    PartnerMismatch,
}
