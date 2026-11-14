using Vivace.Core.Definitions;

namespace Vivace.Tests.Support;

/// <summary>定義の文字列を一時フォルダに書き出す。破棄するとフォルダを消す。</summary>
public sealed class DefinitionFolder : IDisposable
{
    /// <summary>書き出したフォルダのパス。</summary>
    public string Path { get; }

    /// <summary>フォルダを作り、定義のファイルを書き出す。</summary>
    /// <param name="files">定義ファイル（相対パスと中身）。</param>
    public DefinitionFolder(IEnumerable<DefinitionFile> files)
    {
        Path = Directory.CreateTempSubdirectory("vivace-defs-").FullName;
        foreach (var file in files)
        {
            var full = System.IO.Path.Combine(Path, file.Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, file.Text, System.Text.Encoding.UTF8);
        }
    }

    /// <summary>フォルダを消す。</summary>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
