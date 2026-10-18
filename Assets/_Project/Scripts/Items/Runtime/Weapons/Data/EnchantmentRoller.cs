using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器に付くエンチャントを振る。UnityEngine に依存しない。
    ///   1. 確率 chance のサイコロを外れるまで振り、当たった回数だけ付く。1 回ごとにランクの重みでランクを決める。
    ///   2. 最大数を超えたら、ランクの高いものから残す。
    ///   3. ランクの高い順に、武器種のランク表のそのランクの行から 1 つを等確率に選ぶ。同じ種類は 1 つだけ（段違いも不可）。
    ///      そのランクに選べる行が無ければ、それより下で最も高いランクの行から選ぶ。E まで無ければその 1 個は付かない。
    ///   4. 最低数（minCount。お守りなど）に届かなければ、ランクの重みでランクを足して埋める（手順 1 の外れでも最低数は付く）。
    ///      それでも選べる行が無くて足りなければ、残りの行から等確率に足す（種類が尽きたらそこまで）。
    /// ランクは 0 = E … 5 = S の番号で扱う。
    /// </summary>
    public static class EnchantmentRoller
    {
        /// <summary>確率 1 で止まらなくなるのを防ぐ上限。</summary>
        public const int SafetyLimit = 64;

        /// <summary>ランク表の 1 行。Kind は同じ種類かを見分けるだけの番号。</summary>
        public readonly struct Option
        {
            public readonly int Kind;
            public readonly int Rank;

            public Option(int kind, int rank)
            {
                Kind = kind;
                Rank = rank;
            }
        }

        /// <summary>選ばれた options の番号を、ランクの高い順に返す（最低数を埋めるために足した分は後ろに付く）。</summary>
        public static List<int> Roll(IReadOnlyList<Option> options, float chance, IReadOnlyList<float> rankWeights,
            int maxCount, Random random, int minCount = 0)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var result = new List<int>();
            if (options == null || options.Count == 0) return result;

            List<int> ranks = RollRanks(chance, rankWeights, maxCount, random, minCount);
            var usedKinds = new HashSet<int>();
            var candidates = new List<int>();
            foreach (int rank in ranks)
            {
                for (int r = rank; r >= 0; r--)
                {
                    candidates.Clear();
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].Rank == r && !usedKinds.Contains(options[i].Kind)) candidates.Add(i);
                    }

                    if (candidates.Count == 0) continue;

                    int chosen = candidates[random.Next(candidates.Count)];
                    usedKinds.Add(options[chosen].Kind);
                    result.Add(chosen);
                    break;
                }
            }

            // 最低数に届かなかった分（下のランクに行が無かったとき）は、残りの種類から埋める。
            int least = Math.Min(minCount, maxCount);
            while (result.Count < least)
            {
                candidates.Clear();
                for (int i = 0; i < options.Count; i++)
                {
                    if (!usedKinds.Contains(options[i].Kind)) candidates.Add(i);
                }

                if (candidates.Count == 0) break;

                int chosen = candidates[random.Next(candidates.Count)];
                usedKinds.Add(options[chosen].Kind);
                result.Add(chosen);
            }

            return result;
        }

        /// <summary>
        /// 付く数とそれぞれのランク（手順 1・2・4）。高い順に並べ、maxCount 個までに切って返す。
        /// minCount に届かなければランクの重みで足す。重みが全部 0 なら空。chance が 0 以下なら minCount 個だけ。
        /// </summary>
        public static List<int> RollRanks(float chance, IReadOnlyList<float> rankWeights, int maxCount, Random random,
            int minCount = 0)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var ranks = new List<int>();
            double total = 0;
            if (rankWeights != null)
            {
                foreach (float w in rankWeights) total += Math.Max(0f, w);
            }

            if (total <= 0 || maxCount <= 0) return ranks;

            while (chance > 0f && ranks.Count < SafetyLimit && random.NextDouble() < chance)
            {
                ranks.Add(PickRank(rankWeights, total, random));
            }

            int least = Math.Min(Math.Min(minCount, maxCount), SafetyLimit);
            while (ranks.Count < least) ranks.Add(PickRank(rankWeights, total, random));

            ranks.Sort((a, b) => b.CompareTo(a));
            if (ranks.Count > maxCount) ranks.RemoveRange(maxCount, ranks.Count - maxCount);
            return ranks;
        }

        /// <summary>最大数で切る前の、付く数の期待値（幾何分布。平均 p / (1 - p) 個）。</summary>
        public static double ExpectedCount(float chance)
        {
            if (chance <= 0f) return 0;
            if (chance >= 1f) return SafetyLimit;
            return chance / (1.0 - chance);
        }

        private static int PickRank(IReadOnlyList<float> weights, double total, Random random)
        {
            double roll = random.NextDouble() * total;
            int last = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f) continue;
                last = i;
                if (roll < w) return i;
                roll -= w;
            }

            return last;
        }
    }
}
