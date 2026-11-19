using System.Text.Json.Serialization;

namespace Vivace.Core.Events.Guards;

/// <summary>guard の1つの検査。</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ActivityVersionCheck), "activityVersion")]
[JsonDerivedType(typeof(FeasibleCheck), "feasible")]
[JsonDerivedType(typeof(PlaceUnsetCheck), "placeUnset")]
[JsonDerivedType(typeof(PlanVersionCheck), "planVersion")]
[JsonDerivedType(typeof(RngCursorCheck), "rngCursor")]
[JsonDerivedType(typeof(TermUnsetCheck), "termUnset")]
public abstract record GuardCheck;
