using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器の売値。UnityEngine に依存しない。
    ///   売値 = (武器だけの金額 ＋ 武器のランクの金額 ＋ Σ 付いたエンチャントのランクの金額) × 倍率
    /// 武器だけの金額は Weapons.csv の price、ランクの金額は SellPrices.csv。倍率は永続強化の売値の上乗せ（MetaUpgrades.SellPriceMultiplier）。
    /// 四捨五入し、0 を下回らない。
    /// </summary>
    public static class WeaponPrice
    {
        public static int Compute(int basePrice, int weaponRankPrice, IEnumerable<int> enchantmentRankPrices, float multiplier = 1f)
        {
            long sum = Math.Max(0, basePrice) + (long)Math.Max(0, weaponRankPrice);
            if (enchantmentRankPrices != null)
            {
                foreach (int price in enchantmentRankPrices) sum += Math.Max(0, price);
            }

            double total = Math.Round(sum * (double)Math.Max(0f, multiplier), MidpointRounding.AwayFromZero);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
