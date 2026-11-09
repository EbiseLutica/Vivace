namespace Vivace.Core.Definitions;

/// <summary>行動の好み1件。</summary>
/// <param name="Kind">種類。</param>
/// <param name="Factor">倍率（Factor のとき）。</param>
public sealed record Preference(PreferenceKind Kind, double Factor);
