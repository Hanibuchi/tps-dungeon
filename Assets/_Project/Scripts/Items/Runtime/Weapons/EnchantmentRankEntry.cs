using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種のエンチャントのランク表の 1 行。「この種類のこの段は、このランク」。
    /// 正は CSV（Items/Weapons/EnchantmentRanks.csv）で、エディタが取り込んで武器種に書き込む。
    /// </summary>
    [Serializable]
    public struct EnchantmentRankEntry
    {
        [SerializeField] private EnchantmentDefinition definition;

        [SerializeField, Min(1), Tooltip("段（1, 2, 3…）。効果量は 1 段あたりの値 × 段。")]
        private int level;

        [SerializeField, Tooltip("E〜S。ユニークは使わない。")]
        private WeaponRank rank;

        public EnchantmentRankEntry(EnchantmentDefinition definition, int level, WeaponRank rank)
        {
            this.definition = definition;
            this.level = level;
            this.rank = rank;
        }

        public EnchantmentDefinition Definition => definition;
        public int Level => level;
        public WeaponRank Rank => rank;
    }
}
