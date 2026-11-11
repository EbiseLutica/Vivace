namespace Vivace.Core.Domain;

/// <summary>
/// 場所の ID。定義の場所の ID、または生えた場所の ID（例: "kaori_room"）。
/// </summary>
public readonly record struct PlaceId(string Value) : IEntityId
{
    /// <summary>ID の文字列を返す。</summary>
    public override string ToString() => Value;
}
