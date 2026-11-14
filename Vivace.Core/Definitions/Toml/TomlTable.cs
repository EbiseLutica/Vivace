// Tomlyn の多くのプロパティが nullable と注釈されていて、境界での確認を省くため、この file だけ null 注釈を無効にする。
#nullable disable
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn.Syntax;

namespace Vivace.Core.Definitions.Toml;

/// <summary>TOML のテーブル。キーの書かれた順を保つ。</summary>
internal sealed class TomlTable(SourceLocation location) : TomlNode(location)
{
    private readonly List<KeyValuePair<string, TomlNode>> _entries = [];
    private readonly Dictionary<string, TomlNode> _index = [];

    /// <summary>見出し（[name]）で書かれたテーブルかどうか。二度書けば重複になる。</summary>
    public bool HeaderDefined { get; set; }

    /// <summary>キーと値の組を、書かれた順に返す。</summary>
    public IEnumerable<KeyValuePair<string, TomlNode>> Entries => _entries;

    /// <summary>キーがあるかどうか。</summary>
    /// <param name="key">キー。</param>
    public bool ContainsKey(string key) => _index.ContainsKey(key);

    /// <summary>キーの値を引く。</summary>
    /// <param name="key">キー。</param>
    /// <param name="value">見つかった値。</param>
    public bool TryGet(string key, out TomlNode value) => _index.TryGetValue(key, out value);

    /// <summary>キーに値を入れる（同じキーがあれば呼び出し側で確かめておく）。</summary>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    public void Set(string key, TomlNode value)
    {
        _entries.Add(new KeyValuePair<string, TomlNode>(key, value));
        _index[key] = value;
    }

    /// <summary>
    /// 不変の JSON の要素にする（チャンネルの設定など、中身をプラグインに渡すもの）。
    /// </summary>
    public JsonElement ToJson() => JsonSerializer.SerializeToElement(ToJsonNode(this));

    /// <summary>TOML の値を JSON の値に直す。</summary>
    private static JsonNode ToJsonNode(TomlNode node) => node switch
    {
        TomlTable table => ToObject(table),
        TomlArray array => new JsonArray(array.Items.Select(ToJsonNode).ToArray()),
        TomlScalar scalar => scalar.Value switch
        {
            string text => JsonValue.Create(text),
            long number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            bool flag => JsonValue.Create(flag),
            _ => null,
        },
        _ => null,
    };

    /// <summary>TOML のテーブルを JSON のオブジェクトに直す。</summary>
    private static JsonObject ToObject(TomlTable table)
    {
        var json = new JsonObject();
        foreach (var (key, value) in table._entries)
        {
            json[key] = ToJsonNode(value);
        }

        return json;
    }
}
