namespace Vivace.Core.Engine;

/// <summary>不変条件が破れた。エンジンを止める。</summary>
public sealed class InvariantViolationException(string message) : Exception(message);
