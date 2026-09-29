using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器に付くエンチャントを振る。UnityEngine に依存しない。
    /// 個数に上限は無く、「もう 1 個付くか」を一定の確率で振り続ける（幾何分布。平均 p / (1 - p) 個）。
    /// 種類は候補から等確率で選び、同じ種類が重なってもよい（重なったら効果が足される）。
    /// </summary>
    public static class EnchantmentRoller
    {
        /// <summary>確率 1 で止まらなくなるのを防ぐ上限。実質の上限ではない。</summary>
        public const int SafetyLimit = 64;

        /// <summary>
        /// 候補の番号（0 から candidateCount - 1）を、付いた数だけ返す。
        /// 候補が無いか continueChance が 0 以下なら空。
        /// </summary>
        public static List<int> Roll(int candidateCount, float continueChance, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var result = new List<int>();
            if (candidateCount <= 0 || continueChance <= 0f) return result;

            while (result.Count < SafetyLimit && random.NextDouble() < continueChance)
            {
                result.Add(random.Next(candidateCount));
            }

            return result;
        }

        /// <summary>付く個数の期待値。</summary>
        public static double ExpectedCount(float continueChance)
        {
            if (continueChance <= 0f) return 0;
            if (continueChance >= 1f) return SafetyLimit;
            return continueChance / (1.0 - continueChance);
        }
    }
}
