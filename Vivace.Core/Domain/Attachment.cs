namespace Vivace.Core.Domain;

/// <summary>添付。</summary>
/// <param name="Kind">種類（"image" など）。</param>
/// <param name="Url">URL。</param>
/// <param name="Description">説明（あれば）。</param>
public sealed record Attachment(string Kind, string Url, string? Description);
