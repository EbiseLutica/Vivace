// Tomlyn の多くのプロパティが nullable と注釈されていて、境界での確認を省くため、この file だけ null 注釈を無効にする。
#nullable disable
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn.Syntax;

namespace Vivace.Core.Definitions.Toml;

/// <summary>位置を持つ TOML の値の基底。</summary>
internal abstract class TomlNode(SourceLocation location)
{
    /// <summary>値の位置（キーの位置、表の見出しの位置）。</summary>
    public SourceLocation Location { get; } = location;
}

/// <summary>TOML の表。キーの書かれた順を保つ。</summary>
internal sealed class TomlTable(SourceLocation location) : TomlNode(location)
{
    private readonly List<KeyValuePair<string, TomlNode>> _entries = [];
    private readonly Dictionary<string, TomlNode> _index = [];

    /// <summary>見出し（[name]）で書かれた表かどうか。二度書けば重複になる。</summary>
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
    /// 不変の JSON の要素にする（チャンネルの設定など、中身をプラグインに渡すもの。P-21）。
    /// </summary>
    public JsonElement ToJson() => JsonSerializer.SerializeToElement(ToJsonNode(this));

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

/// <summary>TOML の配列。表の配列（[[...]]）のときは IsTableArray を立てる。</summary>
internal sealed class TomlArray(SourceLocation location, bool isTableArray) : TomlNode(location)
{
    /// <summary>要素。</summary>
    public List<TomlNode> Items { get; } = [];

    /// <summary>表の配列（[[...]] で作ったもの）かどうか。</summary>
    public bool IsTableArray { get; } = isTableArray;
}

/// <summary>TOML のスカラー（文字列、整数、実数、真偽値）。</summary>
internal sealed class TomlScalar(SourceLocation location, object value) : TomlNode(location)
{
    /// <summary>値。string、long、double、bool のどれか。</summary>
    public object Value { get; } = value;
}

/// <summary>
/// Tomlyn の構文木から、位置つきの TOML の木を作る。位置は文字の位置（オフセット）から求める。
/// </summary>
internal static class TomlTreeBuilder
{
    /// <summary>
    /// 文字列の TOML を読む。構文のエラーがあれば、木は作らず、エラーの一覧を返す。
    /// </summary>
    /// <param name="text">TOML の文字列。</param>
    /// <param name="path">ファイルのパス（位置に使う）。</param>
    /// <param name="issues">見つかった問題を追加する先。</param>
    public static TomlTable Build(string text, string path, List<DefinitionIssue> issues)
    {
        var lines = new LineIndex(text);
        var document = Tomlyn.Parsing.SyntaxParser.Parse(text, path, false);
        if (document.HasErrors)
        {
            for (var i = 0; i < document.Diagnostics.Count; i++)
            {
                var diagnostic = document.Diagnostics[i];
                if (diagnostic.Kind == DiagnosticMessageKind.Error)
                {
                    issues.Add(new DefinitionIssue(
                        IssueSeverity.Error,
                        DefinitionIssueCode.SyntaxError,
                        lines.At(path, diagnostic.Span.Start.Offset),
                        diagnostic.Message));
                }
            }

            return null;
        }

        return new Builder(path, issues, lines).Build(document);
    }

    /// <summary>
    /// 文字の位置（オフセット）から、行と列（1 から数える）を求める。Tomlyn の Line と Column は使わない。
    /// </summary>
    private sealed class LineIndex
    {
        private readonly int[] _lineStarts;

        public LineIndex(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            _lineStarts = starts.ToArray();
        }

        public SourceLocation At(string path, int offset)
        {
            var index = Array.BinarySearch(_lineStarts, offset);
            if (index < 0)
            {
                index = ~index - 1;
            }

            return new SourceLocation(path, index + 1, offset - _lineStarts[index] + 1);
        }
    }

    private sealed class Builder(string path, List<DefinitionIssue> issues, LineIndex lines)
    {
        public TomlTable Build(DocumentSyntax document)
        {
            var root = new TomlTable(new SourceLocation(path, 1, 1));
            foreach (var keyValue in document.KeyValues)
            {
                SetKeyValue(root, keyValue);
            }

            foreach (var table in document.Tables)
            {
                var names = KeyNames(table.Name);
                var location = Location(table.OpenBracket);
                var parent = root;
                for (var i = 0; i < names.Count - 1; i++)
                {
                    parent = GetOrCreateTable(parent, names[i], location);
                }

                var last = names[^1];
                TomlTable current;
                if (table is TableArraySyntax)
                {
                    current = AppendTableArrayElement(parent, last, location);
                }
                else
                {
                    current = DefineHeaderTable(parent, last, location);
                }

                foreach (var keyValue in table.Items)
                {
                    SetKeyValue(current, keyValue);
                }
            }

            return root;
        }

        private TomlTable DefineHeaderTable(TomlTable parent, string name, SourceLocation location)
        {
            if (parent.TryGet(name, out var existing) && existing is TomlArray)
            {
                Duplicate(name, location);
                return new TomlTable(location);
            }

            var table = GetOrCreateTable(parent, name, location);
            if (table.HeaderDefined)
            {
                Duplicate(name, location);
                return new TomlTable(location);
            }

            table.HeaderDefined = true;
            return table;
        }

        private TomlTable AppendTableArrayElement(TomlTable parent, string name, SourceLocation location)
        {
            var element = new TomlTable(location) { HeaderDefined = true };
            if (!parent.TryGet(name, out var existing))
            {
                var array = new TomlArray(location, isTableArray: true);
                array.Items.Add(element);
                parent.Set(name, array);
                return element;
            }

            if (existing is TomlArray { IsTableArray: true } tableArray)
            {
                tableArray.Items.Add(element);
                return element;
            }

            Duplicate(name, location);
            return element;
        }

        private TomlTable GetOrCreateTable(TomlTable parent, string name, SourceLocation location)
        {
            if (!parent.TryGet(name, out var existing))
            {
                var created = new TomlTable(location);
                parent.Set(name, created);
                return created;
            }

            switch (existing)
            {
                case TomlTable table:
                    return table;
                case TomlArray { IsTableArray: true, Items.Count: > 0 } array when array.Items[^1] is TomlTable last:
                    return last;
                default:
                    Duplicate(name, location);
                    return new TomlTable(location);
            }
        }

        private void SetKeyValue(TomlTable table, KeyValueSyntax keyValue)
        {
            var names = KeyNames(keyValue.Key);
            var container = table;
            for (var i = 0; i < names.Count - 1; i++)
            {
                container = GetOrCreateTable(container, names[i], Location(keyValue.Key));
            }

            var leaf = names[^1];
            var location = Location(keyValue.Key);
            if (container.ContainsKey(leaf))
            {
                Duplicate(leaf, location);
                return;
            }

            container.Set(leaf, ConvertValue(keyValue.Value, location));
        }

        private TomlNode ConvertValue(ValueSyntax value, SourceLocation location)
        {
            switch (value)
            {
                case StringValueSyntax text:
                    return new TomlScalar(location, text.Value);
                case IntegerValueSyntax integer:
                    return new TomlScalar(location, integer.Value);
                case FloatValueSyntax real:
                    return new TomlScalar(location, real.Value);
                case BooleanValueSyntax flag:
                    return new TomlScalar(location, flag.Value);
                case DateTimeValueSyntax date:
                    return new TomlScalar(location, date.Token.Text);
                case ArraySyntax array:
                {
                    var result = new TomlArray(location, isTableArray: false);
                    foreach (var item in array.Items)
                    {
                        result.Items.Add(ConvertValue(item.Value, Location(item.Value)));
                    }

                    return result;
                }
                case InlineTableSyntax inline:
                {
                    var result = new TomlTable(location);
                    foreach (var item in inline.Items)
                    {
                        SetKeyValue(result, item.KeyValue);
                    }

                    return result;
                }
                default:
                    issues.Add(new DefinitionIssue(
                        IssueSeverity.Error,
                        DefinitionIssueCode.InvalidValue,
                        location,
                        "この形の値は使えません。"));
                    return new TomlScalar(location, string.Empty);
            }
        }

        private void Duplicate(string key, SourceLocation location) =>
            issues.Add(new DefinitionIssue(
                IssueSeverity.Error,
                DefinitionIssueCode.DuplicateKey,
                location,
                $"キー \"{key}\" が重複しています。"));

        private SourceLocation Location(SyntaxNodeBase node) => lines.At(path, node.Span.Start.Offset);

        private static List<string> KeyNames(KeySyntax key)
        {
            var names = new List<string> { KeyName(key.Key) };
            foreach (var dotted in key.DotKeys)
            {
                names.Add(KeyName(dotted.Key));
            }

            return names;
        }

        private static string KeyName(BareKeyOrStringValueSyntax key) => key switch
        {
            BareKeySyntax bare => bare.Key.Text,
            StringValueSyntax text => text.Value,
            _ => string.Empty,
        };
    }
}
