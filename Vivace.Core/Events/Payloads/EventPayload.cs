namespace Vivace.Core.Events;

/// <summary>
/// イベントのペイロードの基底。版（"v"）を持つ。項目を足すときは版を上げず、省略可能な項目として足す。
/// </summary>
/// <param name="V">イベントタイプごとの版。</param>
public abstract record EventPayload(int V);
