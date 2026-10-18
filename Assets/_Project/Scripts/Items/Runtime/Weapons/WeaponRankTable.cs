using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ランクごとの見た目と音。UI ではランクを背景色で示す。色は Notion の設定を仮採用している。
    /// 光（オーラ）は材質を入れたランクに付く（今は全ランク。ユニーク以外は粒を減らして明かりも弱い）。落ちたときの音は設定したランク（今はユニークだけ）にしか付かない。
    /// ゲーム全体で 1 つだけ使い、Resources/<see cref="ResourcePath"/> に置く（<see cref="Default"/> で引く）。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponRankTable", menuName = "TPS Dungeon/Weapons/Rank Table")]
    public sealed class WeaponRankTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public WeaponRank rank;
            public Color color;

            [Tooltip("武器にまとわせる光の粒の材質。未設定ならこのランクは光らない。")]
            public Material auraParticles;

            [Tooltip("光の粒と、武器を照らす明かりの色。透明（アルファ 0）ならランクの色を少し明るくした色。")]
            public Color auraColor;

            [Range(0f, 1f), Tooltip("光の粒の量（ユニーク = 1）。粒の数と大きさがこれで減る。")]
            public float auraAmount;

            [Min(0f), Tooltip("武器を照らす明かりの強さ。0 なら明かりは置かない。")]
            public float auraLightIntensity;

            [Tooltip("床に落ちたとき（捨てたときも）に鳴らす音。未設定なら鳴らさない。")]
            public AudioClip dropSound;

            [Range(0f, 1f)] public float dropSoundVolume;
        }

        /// <summary>Resources から読むときのパス（拡張子なし）。</summary>
        public const string ResourcePath = "Weapons/WeaponRankTable";

        private static WeaponRankTable cached;

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>ゲーム全体で使う表。Resources に無ければ null。</summary>
        public static WeaponRankTable Default
        {
            get
            {
                if (cached == null) cached = Resources.Load<WeaponRankTable>(ResourcePath);
                return cached;
            }
        }

        public Color ColorOf(WeaponRank rank) => TryGet(rank, out Entry entry) ? entry.color : Color.gray;

        public bool TryGet(WeaponRank rank, out Entry entry)
        {
            foreach (Entry e in entries)
            {
                if (e.rank != rank) continue;
                entry = e;
                return true;
            }

            entry = default;
            return false;
        }
    }
}
