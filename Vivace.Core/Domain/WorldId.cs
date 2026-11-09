namespace Vivace.Core.Domain;

/// <summary>
/// ワールドインスタンスの ID。定義 ID と同じ文字列（例: "tokyo"）。
/// </summary>
public readonly record struct WorldId(string Value)
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
