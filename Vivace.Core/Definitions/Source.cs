using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>定義の位置。ファイルの相対パス、行と列（1 から数える）。</summary>
/// <param name="Path">ファイルのパス（定義フォルダからの相対）。</param>
/// <param name="Line">行。</param>
/// <param name="Column">列。</param>
public readonly record struct SourceLocation(string Path, int Line, int Column)
{
    /// <summary>"path(行,列)" の形にする。</summary>
    public override string ToString() => $"{Path}({Line},{Column})";
}

/// <summary>
/// 定義の出どころ。表の位置と、表の中の各キーの位置を持つ。参照のエラーは、そのキーの位置に出す。
/// </summary>
/// <param name="Location">表の位置（見出しの位置）。</param>
/// <param name="Keys">キーごとの位置。</param>
public sealed record DefinitionSource(SourceLocation Location, ImmutableDictionary<string, SourceLocation> Keys)
{
    /// <summary>
    /// キーの位置を返す。入れ子のキーは「親.子」の形で引く。そのキーがなければ、いちばん近い親の位置、なければ表の位置。
    /// </summary>
    /// <param name="key">キー（入れ子は「親.子」）。</param>
    public SourceLocation At(string key)
    {
        var path = key;
        while (true)
        {
            if (Keys.TryGetValue(path, out var location))
            {
                return location;
            }

            var dot = path.LastIndexOf('.');
            if (dot < 0)
            {
                return Location;
            }

            path = path[..dot];
        }
    }
}

/// <summary>読み込む定義ファイル1つ。</summary>
/// <param name="Path">
/// 定義フォルダからの相対パス（"worlds/tokyo.toml" など）。組み込みは "builtin/actions/core.toml" の形。
/// フォルダ名で種類を決める。
/// </param>
/// <param name="Text">ファイルの中身。</param>
/// <param name="IsBuiltin">組み込みのファイルかどうか（名前空間 core. を使える）。</param>
public sealed record DefinitionFile(string Path, string Text, bool IsBuiltin = false);

/// <summary>問題の重さ。エラーがあれば読み込まない。</summary>
public enum IssueSeverity
{
    /// <summary>読み込まない。</summary>
    Error,

    /// <summary>読み込むが、知らせる。</summary>
    Warning,
}

/// <summary>問題の種類（D7 7.2）。</summary>
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

    /// <summary>固定予定の隙間が、場所の間の移動時間より短い（警告。D7 7.2、J-147）。</summary>
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

    /// <summary>同行の相手と、時刻、場所、行動が一致しない（警告。D5 5.11）。</summary>
    PartnerMismatch,
}

/// <summary>問題1件。位置つき。</summary>
/// <param name="Severity">重さ。</param>
/// <param name="Code">種類。</param>
/// <param name="Location">位置。</param>
/// <param name="Message">説明（日本語）。</param>
public sealed record DefinitionIssue(IssueSeverity Severity, DefinitionIssueCode Code, SourceLocation Location, string Message)
{
    /// <summary>"位置: 重さ 種類: 説明" の形にする。</summary>
    public override string ToString() => $"{Location}: {Severity} {Code}: {Message}";
}

/// <summary>読み込みの結果。エラーがなければ Definitions がある。</summary>
/// <param name="Definitions">定義。エラーがあれば null。</param>
/// <param name="Issues">エラーと警告の全件。</param>
public sealed record DefinitionLoadResult(Definitions? Definitions, ImmutableArray<DefinitionIssue> Issues)
{
    /// <summary>エラーがあるかどうか。</summary>
    public bool HasErrors => Issues.Any(issue => issue.Severity == IssueSeverity.Error);
}
