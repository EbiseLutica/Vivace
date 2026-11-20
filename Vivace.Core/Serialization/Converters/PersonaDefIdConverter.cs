using Vivace.Core.Domain;

namespace Vivace.Core.Serialization.Converters;

/// <summary>PersonaDefId を文字列の JSON にする。</summary>
public sealed class PersonaDefIdConverter : EntityIdConverter<PersonaDefId>
{
    /// <inheritdoc />
    protected override PersonaDefId Create(string value) => new(value);
}
