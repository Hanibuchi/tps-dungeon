using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// ランクごとの売値の金額（武器のランク・エンチャントのランク）。正は CSV（Settings/Tables/SellPrices.csv）で、保存すると取り込まれてここが書き換わる。
    /// ゲーム全体で 1 つだけ使い、Resources/<see cref="ResourcePath"/> に置く（<see cref="Default"/> で引く）。
    /// ランクの見た目の表（WeaponRankTable）は生成器が書き直すので、こちらは別に持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponPriceTable", menuName = "TPS Dungeon/Weapons/Price Table")]
    public sealed class WeaponPriceTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public WeaponRank rank;

            [Min(0), Tooltip("このランクの武器の売値に足す金額。")]
            public int weapon;

            [Min(0), Tooltip("このランクのエンチャント 1 つにつき売値に足す金額。")]
            public int enchantment;
        }

        /// <summary>Resources から読むときのパス（拡張子なし）。</summary>
        public const string ResourcePath = "Weapons/WeaponPriceTable";

        private static WeaponPriceTable cached;

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>ゲーム全体で使う表。Resources に無ければ null。</summary>
        public static WeaponPriceTable Default
        {
            get
            {
                if (cached == null) cached = Resources.Load<WeaponPriceTable>(ResourcePath);
                return cached;
            }
        }

        /// <summary>ランク rank の武器に足す金額。表に無ければ 0。</summary>
        public int WeaponPriceOf(WeaponRank rank) => TryGet(rank, out Entry entry) ? entry.weapon : 0;

        /// <summary>ランク rank のエンチャント 1 つに足す金額。表に無ければ 0。</summary>
        public int EnchantmentPriceOf(WeaponRank rank) => TryGet(rank, out Entry entry) ? entry.enchantment : 0;

        private bool TryGet(WeaponRank rank, out Entry entry)
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
