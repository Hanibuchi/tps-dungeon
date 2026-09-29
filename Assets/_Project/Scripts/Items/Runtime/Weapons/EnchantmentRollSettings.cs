using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// エンチャントの付き方。ランクに関係なく一定で、全武器種が同じアセットを参照する。
    /// </summary>
    [CreateAssetMenu(fileName = "EnchantmentRollSettings", menuName = "TPS Dungeon/Weapons/Enchantment Roll Settings")]
    public sealed class EnchantmentRollSettings : ScriptableObject
    {
        [SerializeField, Range(0f, 0.95f), Tooltip("エンチャントがもう 1 個付く確率。個数に上限は無い（0.35 なら平均 0.54 個）。値は仮。")]
        private float continueChance = 0.35f;

        public float ContinueChance => continueChance;
    }
}
