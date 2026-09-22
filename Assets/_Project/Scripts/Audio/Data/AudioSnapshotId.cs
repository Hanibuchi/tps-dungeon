namespace TpsDungeon.Audio.Data
{
    /// <summary>
    /// ミキサーのスナップショット。エフェクトの効き具合をまとめて切り替えるための識別子。
    /// 音量 3 種は露出パラメータ側で持つので、スナップショットでは触らない。
    /// </summary>
    public enum AudioSnapshotId
    {
        /// <summary>素の状態。ローパスもリバーブも効かない。</summary>
        Default = 0,

        /// <summary>ダンジョン内。石造りの塔らしい残響が乗る。</summary>
        Dungeon = 1,

        /// <summary>ポーズ中。ローパスで全体がこもる。</summary>
        Paused = 2,
    }
}
