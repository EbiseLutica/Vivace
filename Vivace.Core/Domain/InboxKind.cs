namespace Vivace.Core.Domain;

/// <summary>受信箱のアイテムの大分類。コアはこれで扱い方を決める。</summary>
public enum InboxKind
{
    /// <summary>言葉で話しかけられた（メンション、返信、引用、DM）。</summary>
    Message,

    /// <summary>自分の発言へのリアクション。</summary>
    Reaction,

    /// <summary>フォローされた。</summary>
    Follow,

    /// <summary>上のどれにも当たらない、チャンネル固有のもの。</summary>
    Other,
}
