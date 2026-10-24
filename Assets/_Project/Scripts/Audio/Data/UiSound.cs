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

        /// <summary>札を並べ替えて離した。</summary>
        Place = 7,

        /// <summary>できない操作をした。</summary>
        Denied = 8,

        /// <summary>物を足元に捨てた。</summary>
        Discard = 9,

        /// <summary>札をドラッグしていて、仲間の並びが入れ替わった。</summary>
        Reorder = 10,

        /// <summary>物を仲間の手持ちの枠へ入れた。</summary>
        ToHand = 11,

        /// <summary>物を背嚢へ入れた。</summary>
        ToBag = 12,

        /// <summary>魂の強化の画面で、レベルから生まれたソウルが 1 粒届いた。増える量に応じて何度も続けて鳴るので控えめに。</summary>
        SoulGain = 13,

        /// <summary>死んで得たソウルが、まとめて手持ちのソウルに入った。</summary>
        SoulBank = 14,

        /// <summary>魂の強化を解放した。</summary>
        Unlock = 15,

        /// <summary>解放した強化の先に、新しい強化が現れた。</summary>
        Reveal = 16,

        /// <summary>魂の強化を解放するとき、手持ちのソウルが 1 粒出ていった。払う量に応じて何度も続けて鳴る。</summary>
        SoulSpend = 17,

        /// <summary>持ち越す武器を決めて、選ばなかった武器が砕けた。</summary>
        CarryLost = 18,
    }
}
