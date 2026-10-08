using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ユニークに必ず付くエンチャント 1 つ。種類と段を持つ。
    /// 正は CSV（Items/Weapons/UniqueEnchantments.csv）で、エディタが取り込んで武器に書き込む。
    /// </summary>
    [Serializable]
    public struct FixedEnchantmentEntry
    {
        [SerializeField] private EnchantmentDefinition definition;

        [SerializeField, Min(1), Tooltip("段（1, 2, 3…）。効果量は 1 段あたりの値 × 段。")]
        private int level;

        public FixedEnchantmentEntry(EnchantmentDefinition definition, int level)
        {
            this.definition = definition;
            this.level = level;
        }

        public EnchantmentDefinition Definition => definition;
        public int Level => level;
    }
}
