using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 魂の強化の表。強化の木（ノード）は Settings/Tables/SoulUpgrades.csv をエディタが取り込んで入れる（SoulUpgradeCsvImporter）。
    /// レベルからソウルへの式はこのアセットで調整する（取り込みでは書き換えない）。
    /// Resources に置き、実行中は <see cref="MetaUpgrades"/> が読む。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Soul Upgrade Table", fileName = "SoulUpgrades")]
    public sealed class SoulUpgradeTable : ScriptableObject
    {
        public const string ResourcePath = "Progression/SoulUpgrades";

        [SerializeField] private List<SoulUpgradeNode> nodes = new List<SoulUpgradeNode>();

        [Header("レベルからソウル")]
        [SerializeField, Min(0f), Tooltip("1 人分のソウル = round(この値 × (Lv − 1)^指数)。")]
        private float soulsPerLevel = SoulConversion.DefaultPerLevel;

        [SerializeField, Min(0f), Tooltip("レベルが高いほど多くもらえるようにする指数。1 でレベルに比例。")]
        private float soulExponent = SoulConversion.DefaultExponent;

        public IReadOnlyList<SoulUpgradeNode> Nodes => nodes;
        public float SoulsPerLevel => soulsPerLevel;
        public float SoulExponent => soulExponent;

        /// <summary>木を差し替える（取り込みから）。</summary>
        public void SetNodes(List<SoulUpgradeNode> value)
        {
            nodes = value ?? new List<SoulUpgradeNode>();
            MetaUpgrades.InvalidateTree();
        }

        private void OnValidate() => MetaUpgrades.InvalidateTree();
    }
}
