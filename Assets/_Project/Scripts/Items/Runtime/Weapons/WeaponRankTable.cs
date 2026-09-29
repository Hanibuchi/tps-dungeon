using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ランクごとの見た目。UI ではランクを背景色で示す。色は Notion の設定を仮採用している。
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

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

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
