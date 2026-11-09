using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>生えた場所。</summary>
/// <param name="Id">場所の ID。</param>
/// <param name="Name">表示名。</param>
/// <param name="Tags">場所のタグ。</param>
/// <param name="Group">所属のまとまり（家など）。</param>
/// <param name="Links">つながる場所と移動時間（分）。ID の順に並べる。</param>
/// <param name="Status">状態。</param>
/// <param name="CreatedSeq">生えたときのイベントの連番。</param>
/// <param name="UpdatedSeq">状態が最後に変わったときのイベントの連番。</param>
public sealed record RuntimePlace(
    PlaceId Id,
    string Name,
    ImmutableArray<string> Tags,
    string? Group,
    ImmutableArray<PlaceLink> Links,
    PlaceStatus Status,
    long CreatedSeq,
    long UpdatedSeq);
