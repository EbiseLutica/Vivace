using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>PlaceId を文字列の JSON にする。</summary>
public sealed class PlaceIdConverter : EntityIdConverter<PlaceId>
{
    /// <inheritdoc />
    protected override PlaceId Create(string value) => new(value);
}
