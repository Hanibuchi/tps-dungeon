namespace TpsDungeon.Audio.Data
{
    /// <summary>
    /// 音量スライダー 1 本に対応する系統。AudioMixer のグループと 1 対 1 で対応する。
    /// </summary>
    public enum AudioChannel
    {
        /// <summary>全体音量。Master グループ。</summary>
        Master = 0,

        /// <summary>BGM の音量。</summary>
        Bgm = 1,

        /// <summary>効果音の音量。</summary>
        Se = 2,
    }
}
