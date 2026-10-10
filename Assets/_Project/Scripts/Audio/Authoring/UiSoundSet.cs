using System;
using TpsDungeon.Audio.Data;
using UnityEngine;

namespace TpsDungeon.Audio.Authoring
{
    /// <summary>
    /// UI の操作音（<see cref="UiSound"/>）ごとのクリップと音量。GameAudioConfig から参照する。
    /// 素材（効果音ラボ）は ThirdParty に置いてあり git に上げないので、取ってきていない環境ではクリップが空になり、黙って鳴らない。
    /// 差し替えはこのアセットのクリップを入れ替えるだけでよい。
    /// </summary>
    [CreateAssetMenu(fileName = "UiSounds", menuName = "TPS Dungeon/Audio/UI Sound Set")]
    public sealed class UiSoundSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume;

            public Entry(float volume)
            {
                clip = null;
                this.volume = volume;
            }
        }

        [SerializeField, Tooltip("ボタンや枠にポインタを合わせたとき。何度も鳴るので控えめに。")]
        private Entry hover = new Entry(0.5f);

        [SerializeField, Tooltip("ボタンを押したとき。")]
        private Entry click = new Entry(0.8f);

        [SerializeField, Tooltip("一つ前の画面へ戻ったとき。")]
        private Entry back = new Entry(0.8f);

        [SerializeField, Tooltip("メニュー（ポーズ・インベントリ）を開いたとき。")]
        private Entry open = new Entry(0.7f);

        [SerializeField, Tooltip("メニューを閉じたとき。")]
        private Entry close = new Entry(0.6f);

        [SerializeField, Tooltip("タブを切り替えたとき。")]
        private Entry tab = new Entry(0.7f);

        [SerializeField, Tooltip("インベントリで物や札をつかんだとき。")]
        private Entry pick = new Entry(0.8f);

        [SerializeField, Tooltip("物を移した・装備した・札を並べ替えたとき。")]
        private Entry place = new Entry(0.8f);

        [SerializeField, Tooltip("できない操作をしたとき。")]
        private Entry denied = new Entry(0.6f);

        [SerializeField, Tooltip("物を足元に捨てたとき。")]
        private Entry discard = new Entry(0.7f);

        [SerializeField, Min(0f), Tooltip("同じ音をこれより短い間隔では鳴らし直さない（秒）。枠の上を素早くなぞったときに音が団子にならないように。")]
        private float minInterval = 0.04f;

        public float MinInterval => minInterval;

        public Entry Get(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.Hover: return hover;
                case UiSound.Click: return click;
                case UiSound.Back: return back;
                case UiSound.Open: return open;
                case UiSound.Close: return close;
                case UiSound.Tab: return tab;
                case UiSound.Pick: return pick;
                case UiSound.Place: return place;
                case UiSound.Denied: return denied;
                case UiSound.Discard: return discard;
                default: return default;
            }
        }
    }
}
