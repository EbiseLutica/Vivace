namespace Vivace.Core.Events;

/// <summary>受信箱の1件の結果。</summary>
/// <param name="ItemSeq">捌いた通知の連番。</param>
/// <param name="Result">結果。</param>
public sealed record InboxOutcome(long ItemSeq, InboxResult Result);
