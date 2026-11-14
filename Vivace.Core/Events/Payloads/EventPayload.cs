namespace Vivace.Core.Events.Payloads;

/// <summary>
/// イベントのペイロードの基底。バージョン（"v"）を持つ。項目を足すときはバージョンを上げず、省略可能な項目として足す。
/// </summary>
/// <param name="V">イベントタイプごとのバージョン。</param>
public abstract record EventPayload(int V);
