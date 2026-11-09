using System.Runtime.CompilerServices;

namespace Vivace.Tests.Support;

/// <summary>
/// スナップショットの比較。Verify を呼ぶのは、このヘルパーだけ。
/// テストは、文字の列を渡すだけにする。
/// </summary>
public static class Snapshot
{
    /// <summary>文字の列を、保存された期待値と比べる。</summary>
    /// <param name="text">比べる文字の列。</param>
    /// <param name="sourceFile">テストのファイル（期待値の場所を決める）。</param>
    public static Task Text(string text, [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(text, sourceFile: sourceFile);

    /// <summary>値を、保存された期待値と比べる（型の形で書き出す）。</summary>
    /// <param name="target">比べる値。</param>
    /// <param name="sourceFile">テストのファイル（期待値の場所を決める）。</param>
    public static Task Object(object target, [CallerFilePath] string sourceFile = "") =>
        Verifier.Verify(target, sourceFile: sourceFile);
}
