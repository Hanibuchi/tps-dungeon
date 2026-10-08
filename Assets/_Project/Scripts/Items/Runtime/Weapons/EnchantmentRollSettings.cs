using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// エンチャントの付き方。武器のランクに関係なく一定で、全武器種が同じアセットを参照する。
    /// 付く数とそれぞれのランクをここで決め、どの種類・段が付くかは武器種のランク表で決まる（<see cref="EnchantmentRoller"/>）。
    /// </summary>
    [CreateAssetMenu(fileName = "EnchantmentRollSettings", menuName = "TPS Dungeon/Weapons/Enchantment Roll Settings")]
    public sealed class EnchantmentRollSettings : ScriptableObject
    {
        /// <summary>エンチャントのランクの数（E〜S）。</summary>
        public const int RankCount = (int)WeaponRank.S + 1;

        [SerializeField, Range(0f, 0.95f), Tooltip("エンチャントが付く確率。外れるまで振り続け、当たった回数だけ付く（最大数まで）。値は仮。")]
        private float continueChance = 0.35f;

        [SerializeField, Min(0), Tooltip("1 本に付くエンチャントの最大数。振って超えたら、ランクの高いものから残す。")]
        private int maxEnchantments = 5;

        [SerializeField, Tooltip("ランク E・D・C・B・A・S の重み（この比でランクが決まる）。値は仮。")]
        private float[] rankWeights = { 0.30f, 0.25f, 0.20f, 0.12f, 0.08f, 0.05f };

        public float ContinueChance => continueChance;
        public int MaxEnchantments => maxEnchantments;

        /// <summary>E から S の順の重み。足りない分は 0。</summary>
        public IReadOnlyList<float> RankWeights
        {
            get
            {
                var weights = new float[RankCount];
                if (rankWeights != null) Array.Copy(rankWeights, weights, Math.Min(rankWeights.Length, RankCount));
                return weights;
            }
        }

        private void OnValidate()
        {
            if (rankWeights == null || rankWeights.Length != RankCount) Array.Resize(ref rankWeights, RankCount);
            for (int i = 0; i < rankWeights.Length; i++) rankWeights[i] = Mathf.Max(0f, rankWeights[i]);
        }
    }
}
