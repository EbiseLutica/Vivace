namespace Vivace.Core.Events;

/// <summary>
/// イベントの出所の大分類（D2 2.9）。
/// </summary>
public enum OriginKind
{
    /// <summary>通常の運転で、Apply かハンドラが積んだ。</summary>
    Sim,

    /// <summary>追いつきの間に積んだ。</summary>
    Catchup,

    /// <summary>管理者が窓口から投入した。</summary>
    Admin,

    /// <summary>テストやドライランで注入した。</summary>
    Test,

    /// <summary>チャンネルの到着（名前がチャンネルの種類）。</summary>
    Channel,

    /// <summary>プラグインが窓口から投入した（名前がプラグインの名前）。</summary>
    Plugin,
}
