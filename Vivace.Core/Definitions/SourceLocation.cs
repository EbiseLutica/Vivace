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
