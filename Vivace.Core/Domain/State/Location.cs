using System.Text.Json.Serialization;

namespace Vivace.Core.Domain.State;

/// <summary>ペルソナの居場所。場所にいるか、移動中かのどちらか。</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AtPlace), "atPlace")]
[JsonDerivedType(typeof(Traveling), "traveling")]
public abstract record Location;
