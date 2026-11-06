namespace Vivace.Core.Domain;

/// <summary>
/// ワールドインスタンスの ID。定義 ID と同じ文字列（例: "tokyo"）。
/// </summary>
public readonly record struct WorldId(string Value)
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}

/// <summary>
/// ペルソナインスタンスの ID。「定義 ID@ワールド ID」の形（例: "kaori@tokyo"）。
/// </summary>
public readonly record struct PersonaId(string Value)
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}

/// <summary>
/// 場所の ID。定義の場所の ID、または生えた場所の ID（例: "kaori_room"）。
/// </summary>
public readonly record struct PlaceId(string Value)
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
