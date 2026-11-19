namespace Vivace.Storage.Sqlite;

/// <summary>スキーマの変更の1件。番号を上げるたびに、1回だけ適用する。</summary>
/// <param name="Version">版の番号（1 から始まる連番）。PRAGMA user_version に入る。</param>
/// <param name="Name">名前（ファイル名の番号の後ろ。ログとエラーに使う）。</param>
/// <param name="Sql">適用する SQL。</param>
public sealed record SqliteMigration(int Version, string Name, string Sql);
