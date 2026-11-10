namespace Vivace.Core.Domain;

/// <summary>プランの由来。</summary>
public enum PlanSource
{
    /// <summary>LLM の Planner が作った。</summary>
    Llm,

    /// <summary>固定予定だけのプラン（代わりの出力）。</summary>
    Fallback,
}
