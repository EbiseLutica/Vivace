// Tomlyn の多くのプロパティが nullable と注釈されていて、境界での確認を省くため、この file だけ null 注釈を無効にする。
#nullable disable
using System.Text.Json.Nodes;
using Tomlyn.Syntax;

namespace Vivace.Core.Definitions.Toml;

/// <summary>TOML の配列。表の配列（[[...]]）のときは IsTableArray を立てる。</summary>
internal sealed class TomlArray(SourceLocation location, bool isTableArray) : TomlNode(location)
{
    /// <summary>要素。</summary>
    public List<TomlNode> Items { get; } = [];

    /// <summary>表の配列（[[...]] で作ったもの）かどうか。</summary>
    public bool IsTableArray { get; } = isTableArray;
}
