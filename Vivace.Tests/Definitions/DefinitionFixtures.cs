using Vivace.Core.Definitions;
using Vivace.Testing;

namespace Vivace.Tests.Definitions;

/// <summary>定義のテストで使う、テスト用の世界の書き換えと位置の計算。</summary>
internal static class DefinitionFixtures
{
    /// <summary>テスト用の世界のファイルのうち、1か所だけを書き換えたものを返す。</summary>
    /// <param name="path">書き換えるファイルのパス。</param>
    /// <param name="from">書き換える文字列（ファイルの中で1回だけ現れること）。</param>
    /// <param name="to">書き換えた後の文字列。</param>
    public static List<DefinitionFile> Replaced(string path, string from, string to)
    {
        var files = TestWorld.Files().ToList();
        var index = files.FindIndex(file => file.Path == path);
        var text = files[index].Text;
        if (text.Split(from).Length != 2)
        {
            throw new InvalidOperationException($"書き換えの対象が1か所ではありません: {path}: {from}");
        }

        files[index] = files[index] with { Text = text.Replace(from, to, StringComparison.Ordinal) };
        return files;
    }

    /// <summary>テスト用の世界のファイルを、そのまま返す。</summary>
    public static List<DefinitionFile> Original() => TestWorld.Files().ToList();

    /// <summary>
    /// 書き換えた後のファイルの中で、文字列の先頭の位置（ファイル、行、列）を返す。
    /// </summary>
    /// <param name="files">読み込みの入力。</param>
    /// <param name="path">ファイルのパス。</param>
    /// <param name="needle">探す文字列（ファイルの中で1回だけ現れること）。</param>
    public static SourceLocation At(IEnumerable<DefinitionFile> files, string path, string needle)
    {
        var text = files.Single(file => file.Path == path).Text;
        if (text.Split(needle).Length != 2)
        {
            throw new InvalidOperationException($"探す文字列が1か所ではありません: {path}: {needle}");
        }

        var index = text.IndexOf(needle, StringComparison.Ordinal);
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return new SourceLocation(path, line, index - lineStart + 1);
    }

    /// <summary>問題の一覧から、コードと位置が一致するものを探す。</summary>
    /// <param name="result">読み込みの結果。</param>
    /// <param name="code">種類。</param>
    /// <param name="location">位置。</param>
    public static DefinitionIssue Find(DefinitionLoadResult result, DefinitionIssueCode code, SourceLocation location)
    {
        var matches = result.Issues.Where(issue => issue.Code == code && issue.Location == location).ToList();
        Assert.True(
            matches.Count > 0,
            $"種類 {code} の問題が {location} にありません。見つかったもの: {string.Join(" | ", result.Issues.Select(issue => issue.ToString()))}");
        return matches[0];
    }
}
