using System.Collections.Immutable;

namespace Vivace.Core.Definitions;

/// <summary>
/// 定義の出どころ。テーブルの位置と、テーブルの中の各キーの位置を持つ。参照のエラーは、そのキーの位置に出す。
/// </summary>
/// <param name="Location">テーブルの位置（見出しの位置）。</param>
/// <param name="Keys">キーごとの位置。</param>
public sealed record DefinitionSource(SourceLocation Location, ImmutableDictionary<string, SourceLocation> Keys)
{
    /// <summary>
    /// キーの位置を返す。入れ子のキーは「親.子」の形で引く。そのキーがなければ、いちばん近い親の位置、なければテーブルの位置。
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
