namespace Vivace.Core.Definitions.Model;

/// <summary>反射のしきい値と行動。</summary>
/// <param name="Threshold">しきい値。越えた瞬間に反射する。</param>
/// <param name="ActionId">反射で始める行動の定義 ID。</param>
/// <param name="Source">位置。</param>
public sealed record ReflexDef(double Threshold, string ActionId, SourceLocation Source);
