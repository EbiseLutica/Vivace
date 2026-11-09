// Tomlyn の多くのプロパティが nullable と注釈されていて、境界での確認を省くため、この file だけ null 注釈を無効にする。
#nullable disable
using System.Text.Json.Nodes;
using Tomlyn.Syntax;

namespace Vivace.Core.Definitions.Toml;

/// <summary>TOML のスカラー（文字列、整数、実数、真偽値）。</summary>
internal sealed class TomlScalar(SourceLocation location, object value) : TomlNode(location)
{
    /// <summary>値。string、long、double、bool のどれか。</summary>
    public object Value { get; } = value;
}
