using System.Collections.Immutable;
using Vivace.Core.Time;

namespace Vivace.Core.Definitions.Toml;

/// <summary>
/// 表の項目を型つきで読む。読んだキーを覚え、最後に Done で読まなかったキーを警告する。
/// 型が合わないときや必須の項目がないときは、位置つきのエラーを積む。
/// </summary>
internal sealed class TableReader(TomlTable table, List<DefinitionIssue> issues)
{
    private readonly HashSet<string> _used = [];

    /// <summary>読む表の位置。</summary>
    public SourceLocation Location => table.Location;

    /// <summary>問題の集め先。子の表を読む TableReader に渡す。</summary>
    public List<DefinitionIssue> Issues => issues;

    /// <summary>キーの値を、TOML の配列として読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public IReadOnlyList<TomlNode> Array(string key)
    {
        if (!TryNode(key, out var node))
        {
            return [];
        }

        if (node is TomlArray array)
        {
            return array.Items;
        }

        TypeError(node, key, "配列");
        return [];
    }

    /// <summary>キーの値を、生のまま引く。読んだものとして印を付ける。</summary>
    /// <param name="key">キー。</param>
    /// <param name="node">見つかった値。</param>
    public bool TryNode(string key, out TomlNode node)
    {
        _used.Add(key);
        return table.TryGet(key, out node);
    }

    /// <summary>文字列を読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public string? String(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlScalar { Value: string text })
        {
            return text;
        }

        TypeError(node, key, "文字列");
        return null;
    }

    /// <summary>テキストを値に読む関数。</summary>
    /// <typeparam name="T">読んだ値の型。</typeparam>
    /// <param name="text">テキスト。</param>
    /// <param name="value">読めたときの値。</param>
    public delegate bool TextParser<T>(string text, out T value);

    /// <summary>
    /// 文字列を、読み方の関数で値にする。読めなければ、期待する形と一緒にエラー。
    /// </summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="key">キー。</param>
    /// <param name="parser">読み方。</param>
    /// <param name="expected">期待する形の説明。</param>
    public T? Parsed<T>(string key, TextParser<T> parser, string expected) where T : struct
    {
        var text = String(key);
        if (text is null)
        {
            return null;
        }

        if (parser(text, out var value))
        {
            return value;
        }

        InvalidAt(key, $"{key} は{expected}で書いてください: \"{text}\"");
        return null;
    }

    /// <summary>
    /// 必須の文字列を読み方の関数で値にする。キーがなければ必須のエラー、読めなければ形式のエラー。
    /// </summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="key">キー。</param>
    /// <param name="parser">読み方。</param>
    /// <param name="expected">期待する形の説明。</param>
    public T? RequiredParsed<T>(string key, TextParser<T> parser, string expected) where T : struct
    {
        if (!table.ContainsKey(key))
        {
            Missing(key);
            _used.Add(key);
            return null;
        }

        return Parsed(key, parser, expected);
    }

    /// <summary>キーの位置を返す。キーがなければ表の位置。</summary>
    /// <param name="key">キー。</param>
    public SourceLocation LocationOf(string key) =>
        table.TryGet(key, out var node) ? node.Location : table.Location;

    /// <summary>キーの値の位置にエラーを積む。</summary>
    /// <param name="key">キー。</param>
    /// <param name="message">説明。</param>
    public void InvalidAt(string key, string message) => Invalid(LocationOf(key), message);

    /// <summary>必須の文字列を読む。なければエラー。</summary>
    /// <param name="key">キー。</param>
    public string? RequiredString(string key)
    {
        if (!table.ContainsKey(key))
        {
            Missing(key);
            _used.Add(key);
            return null;
        }

        return String(key);
    }

    /// <summary>整数を読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public long? Integer(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlScalar { Value: long number })
        {
            return number;
        }

        TypeError(node, key, "整数");
        return null;
    }

    /// <summary>実数（整数も可）を読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public double? Number(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        switch (node)
        {
            case TomlScalar { Value: long integer }:
                return integer;
            case TomlScalar { Value: double real }:
                return real;
            default:
                TypeError(node, key, "数");
                return null;
        }
    }

    /// <summary>真偽値を読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public bool? Bool(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlScalar { Value: bool flag })
        {
            return flag;
        }

        TypeError(node, key, "真偽値");
        return null;
    }

    /// <summary>文字列の配列を読む。要素が文字列でなければエラー。</summary>
    /// <param name="key">キー。</param>
    public ImmutableArray<string> Strings(string key)
    {
        if (!TryNode(key, out var node))
        {
            return ImmutableArray<string>.Empty;
        }

        if (node is not TomlArray array)
        {
            TypeError(node, key, "文字列の配列");
            return ImmutableArray<string>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var item in array.Items)
        {
            if (item is TomlScalar { Value: string text })
            {
                result.Add(text);
            }
            else
            {
                TypeError(item, key, "文字列");
            }
        }

        return result.ToImmutable();
    }

    /// <summary>表を読む。型が違えばエラー。</summary>
    /// <param name="key">キー。</param>
    public TomlTable? Table(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlTable child)
        {
            return child;
        }

        TypeError(node, key, "表");
        return null;
    }

    /// <summary>表の配列（[[key]]）を読む。なければ空。</summary>
    /// <param name="key">キー。</param>
    public IReadOnlyList<TomlTable> TableArray(string key)
    {
        if (!TryNode(key, out var node))
        {
            return [];
        }

        if (node is TomlArray { IsTableArray: true } array)
        {
            return array.Items.OfType<TomlTable>().ToList();
        }

        TypeError(node, key, "表の配列");
        return [];
    }

    /// <summary>
    /// 時間の長さを読む。文字列なら固定、2 要素の配列なら範囲（最短、最長）。
    /// </summary>
    /// <param name="key">キー。</param>
    public ActionDurationReadResult? Duration(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlScalar { Value: string text } && TimeTextHelper.TryParseDuration(text, out var fixedLength))
        {
            return new ActionDurationReadResult(fixedLength, fixedLength, false);
        }

        if (node is TomlArray { Items.Count: 2 } range
            && range.Items[0] is TomlScalar { Value: string minText }
            && range.Items[1] is TomlScalar { Value: string maxText }
            && TimeTextHelper.TryParseDuration(minText, out var min)
            && TimeTextHelper.TryParseDuration(maxText, out var max))
        {
            if (min > max)
            {
                Invalid(node, $"範囲の最短が最長より長くなっています: {key}");
            }

            return new ActionDurationReadResult(min, max, true);
        }

        Invalid(node, $"時間の長さは \"45m\" の形か、2 要素の配列で書いてください: {key}");
        return null;
    }

    /// <summary>
    /// 時間の文字列を読む（"45m"）。形が違えばエラー。
    /// </summary>
    /// <param name="key">キー。</param>
    public TimeSpan? Span(string key)
    {
        var text = String(key);
        if (text is null)
        {
            return null;
        }

        if (TimeTextHelper.TryParseDuration(text, out var span))
        {
            return span;
        }

        Invalid(table.TryGet(key, out var node) ? node : null, $"時間の長さの形が正しくありません: {key} = \"{text}\"");
        return null;
    }

    /// <summary>
    /// 時刻の範囲（["06:00", "23:00"]）を読む。形が違えばエラー。
    /// </summary>
    /// <param name="key">キー。</param>
    public TimeRange? Hours(string key)
    {
        if (!TryNode(key, out var node))
        {
            return null;
        }

        if (node is TomlArray { Items.Count: 2 } array
            && array.Items[0] is TomlScalar { Value: string fromText }
            && array.Items[1] is TomlScalar { Value: string toText }
            && TimeTextHelper.TryParseTimeOfDay(fromText, out var from)
            && TimeTextHelper.TryParseTimeOfDay(toText, out var to))
        {
            return new TimeRange(from, to);
        }

        Invalid(node, $"時刻の範囲は [\"HH:mm\", \"HH:mm\"] の形で書いてください: {key}");
        return null;
    }

    /// <summary>
    /// 読まなかったキーを警告する。すべて読み終えてから呼ぶ。
    /// </summary>
    public void Done()
    {
        foreach (var (key, node) in table.Entries)
        {
            if (!_used.Contains(key))
            {
                issues.Add(new DefinitionIssue(
                    IssueSeverity.Warning,
                    DefinitionIssueCode.UnknownKey,
                    node.Location,
                    $"未知のキーです（無視します）: {key}"));
            }
        }
    }

    /// <summary>必須の項目がないエラーを積む（表の位置）。</summary>
    /// <param name="key">キー。</param>
    public void Missing(string key) =>
        issues.Add(new DefinitionIssue(
            IssueSeverity.Error,
            DefinitionIssueCode.MissingRequired,
            table.Location,
            $"必須の項目がありません: {key}"));

    /// <summary>値の範囲や形式のエラーを積む。位置が不明なら表の位置を使う。</summary>
    /// <param name="node">問題の値。</param>
    /// <param name="message">説明。</param>
    public void Invalid(TomlNode? node, string message) =>
        issues.Add(new DefinitionIssue(
            IssueSeverity.Error,
            DefinitionIssueCode.InvalidValue,
            node?.Location ?? table.Location,
            message));

    /// <summary>値の形式のエラーを、位置を指定して積む。</summary>
    /// <param name="location">位置。</param>
    /// <param name="message">説明。</param>
    public void Invalid(SourceLocation location, string message) =>
        issues.Add(new DefinitionIssue(IssueSeverity.Error, DefinitionIssueCode.InvalidValue, location, message));

    /// <summary>エラーを、種類を指定して積む。</summary>
    /// <param name="code">種類。</param>
    /// <param name="location">位置。</param>
    /// <param name="message">説明。</param>
    public void Error(DefinitionIssueCode code, SourceLocation location, string message) =>
        issues.Add(new DefinitionIssue(IssueSeverity.Error, code, location, message));

    /// <summary>型が違うことをエラーにする。</summary>
    private void TypeError(TomlNode node, string key, string expected) =>
        Invalid(node, $"{key} は{expected}で書いてください。");
}
