using System.IO;
using Microsoft.Data.Sqlite;

namespace Vivace.Tests.Storage;

/// <summary>テスト用の一時フォルダ（その中の SQLite のファイル）。破棄するとフォルダごと消す。</summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _folder;

    /// <summary>新しい一時フォルダを作り、その中のファイルのパスを決める。</summary>
    public TempDatabase()
    {
        _folder = Path.Combine(Path.GetTempPath(), "vivace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        FilePath = Path.Combine(_folder, "vivace.db");
    }

    /// <summary>データベースのファイルのパス。</summary>
    public string FilePath { get; }

    /// <summary>接続のプールを空にし、フォルダを消す。開いているデータベースは先に閉じておくこと。</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
