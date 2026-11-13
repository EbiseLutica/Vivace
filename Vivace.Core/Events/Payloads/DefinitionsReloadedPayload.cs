namespace Vivace.Core.Events.Payloads;

/// <summary>定義の読み直し。どの定義で動いているかの記録。</summary>
/// <param name="Hash">定義全体のハッシュ。</param>
public sealed record DefinitionsReloadedPayload(string Hash) : EventPayload(1);
