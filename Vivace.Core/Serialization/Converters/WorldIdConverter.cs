using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>WorldId を文字列の JSON にする。</summary>
public sealed class WorldIdConverter : EntityIdConverter<WorldId>
{
    /// <inheritdoc />
    protected override WorldId Create(string value) => new(value);
}
