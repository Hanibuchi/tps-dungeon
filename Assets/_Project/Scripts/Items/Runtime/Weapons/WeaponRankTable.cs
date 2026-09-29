using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ランクごとの見た目。UI ではランクを背景色で示す。色は Notion の設定を仮採用している。
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

        public Color ColorOf(WeaponRank rank)
        {
            foreach (Entry entry in entries)
            {
                if (entry.rank == rank) return entry.color;
            }

            return Color.gray;
        }
    }
}
