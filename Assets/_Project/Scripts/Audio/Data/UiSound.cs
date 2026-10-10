namespace TpsDungeon.Audio.Data
{
    /// <summary>
    /// UI の操作音の種類。どの音を鳴らすかは <see cref="Authoring.UiSoundSet"/> が持つ。
    /// 鳴らすのは <see cref="Runtime.GameAudio.PlayUi"/>。ボタンの合わせ・押下は <see cref="UI.UiSoundHooks"/> が拾う。
    /// </summary>
    public enum UiSound
    {
        /// <summary>ボタンや枠にポインタを合わせた。</summary>
        Hover = 0,

        /// <summary>ボタンを押した。</summary>
        Click = 1,

        /// <summary>一つ前の画面へ戻った（設定 → ポーズメニューなど）。</summary>
        Back = 2,

        /// <summary>メニュー（ポーズ・インベントリ）を開いた。</summary>
        Open = 3,

        /// <summary>メニューを閉じた。</summary>
        Close = 4,

        /// <summary>タブを切り替えた。</summary>
        Tab = 5,

        /// <summary>インベントリで物や札をつかんだ（ドラッグを始めた）。</summary>
        Pick = 6,

        /// <summary>物を移した・装備した・札を並べ替えた。</summary>
        Place = 7,

        /// <summary>できない操作をした。</summary>
        Denied = 8,

        /// <summary>物を足元に捨てた。</summary>
        Discard = 9,
    }
}
