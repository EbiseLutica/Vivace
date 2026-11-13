using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;

namespace Vivace.Testing;

/// <summary>
/// テスト用の小さな世界の定義。1ワールド、数か所の場所、2人のペルソナ、数個の行動と出来事。
/// 定義は埋め込みの TOML（Defs/）から読む。
/// </summary>
public static class TestWorld
{
    /// <summary>テスト用のワールドの定義 ID。</summary>
    public static readonly WorldDefId DefId = new("testville");

    /// <summary>埋め込みのテスト用の定義ファイルを、読み込みの入力の形で返す。</summary>
    public static ImmutableArray<DefinitionFile> Files()
    {
        var assembly = typeof(TestWorld).Assembly;
        var files = ImmutableArray.CreateBuilder<DefinitionFile>();
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("defs/", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"埋め込みのテスト定義が見つかりません: {name}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            files.Add(new DefinitionFile(name["defs/".Length..], reader.ReadToEnd()));
        }

        return files.ToImmutable();
    }

    /// <summary>テスト用の定義を読み込む（問題の一覧を含む）。</summary>
    public static DefinitionLoadResult Load() => DefinitionLoader.LoadFiles(Files());

    /// <summary>テスト用の定義を読む。エラーがあれば例外。</summary>
    public static DefinitionPack LoadOrThrow() => Shared.Value;

    private static readonly Lazy<DefinitionPack> Shared = new(() =>
    {
        var result = Load();
        return result.Definitions ?? throw new InvalidOperationException(
            "テスト用の定義にエラーがあります: " + string.Join("; ", result.Issues.Select(issue => issue.ToString())));
    });
}
