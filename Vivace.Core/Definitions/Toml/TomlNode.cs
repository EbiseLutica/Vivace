// Tomlyn の多くのプロパティが nullable と注釈されていて、境界での確認を省くため、この file だけ null 注釈を無効にする。
#nullable disable
using System.Text.Json.Nodes;
using Tomlyn.Syntax;
using Vivace.Core.Domain.State;

namespace Vivace.Core.Definitions.Toml;

/// <summary>位置を持つ TOML の値の基底。</summary>
internal abstract class TomlNode(SourceLocation location)
{
    /// <summary>値の位置（キーの位置、表の見出しの位置）。</summary>
    public SourceLocation Location { get; } = location;
}
