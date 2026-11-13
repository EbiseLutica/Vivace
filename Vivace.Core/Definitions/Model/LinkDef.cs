namespace Vivace.Core.Definitions.Model;

/// <summary>場所のつながり（片方に書けば逆向きにも張る）。</summary>
/// <param name="To">つながる場所の ID。</param>
/// <param name="Minutes">移動時間（分）。</param>
/// <param name="Source">位置。</param>
public sealed record LinkDef(string To, int Minutes, SourceLocation Source);
