namespace Vivace.Core.Definitions;

/// <summary>読み込む定義ファイル1つ。</summary>
/// <param name="Path">
/// 定義フォルダからの相対パス（"worlds/tokyo.toml" など）。組み込みは "builtin/actions/core.toml" の形。
/// フォルダ名で種類を決める。
/// </param>
/// <param name="Text">ファイルの中身。</param>
/// <param name="IsBuiltin">組み込みのファイルかどうか（名前空間 core. を使える）。</param>
public sealed record DefinitionFile(string Path, string Text, bool IsBuiltin = false);
