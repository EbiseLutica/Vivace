using System.Globalization;
using Vivace.Core.Definitions;

namespace Vivace.Cli;

/// <summary>定義のフォルダを読み、問題を1行ずつ書き、件数を書く。</summary>
public static class ValidateCommand
{
    /// <summary>定義のフォルダを検証する。エラーがあれば 1、なければ 0 を返す。</summary>
    /// <param name="dir">定義のフォルダ。</param>
    /// <param name="output">出力先。</param>
    public static int Run(string dir, TextWriter output)
    {
        if (!Directory.Exists(dir))
        {
            output.WriteLine($"定義のフォルダがありません: {dir}");
            return 1;
        }

        return Write(DefinitionLoader.LoadDirectory(dir), output);
    }

    /// <summary>読み込みの結果の問題を書き、件数を書く。エラーがあれば 1、なければ 0 を返す。</summary>
    /// <param name="result">読み込みの結果。</param>
    /// <param name="output">出力先。</param>
    public static int Write(DefinitionLoadResult result, TextWriter output)
    {
        foreach (var issue in result.Issues)
        {
            output.WriteLine(issue.ToString());
        }

        var errors = result.Issues.Count(issue => issue.Severity == IssueSeverity.Error);
        var warnings = result.Issues.Count(issue => issue.Severity == IssueSeverity.Warning);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"エラー {errors} 件、警告 {warnings} 件"));
        return result.HasErrors ? 1 : 0;
    }
}
