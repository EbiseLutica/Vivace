namespace Vivace.Core.Llm;

/// <summary>LLM の応答。</summary>
/// <typeparam name="T">応答の値の型。</typeparam>
/// <param name="Value">応答の値。</param>
/// <param name="Usage">使用量。</param>
public sealed record LlmResult<T>(T Value, LlmUsage Usage);
