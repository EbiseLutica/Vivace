using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vivace.Core.Serialization.Converters;

/// <summary>
/// 時刻を UTC の ISO 8601（精度を落とさない形）で読み書きする。読むときはオフセット 0 になる。
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString() ?? throw new JsonException("時刻が null です。"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}
