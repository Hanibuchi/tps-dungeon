using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 倒した敵が落とす武器を決める。UnityEngine に依存しない。1 体につき落ちるのは多くて 1 本。
    ///   1. ユニークの行を見る。天井に届いた行（出ないまま倒した数 + 1 ≥ 天井）があればその最初の行で確定。
    ///      無ければ上の行から順に「確率 × 倍率」で振り、最初に当たった行。
    ///      落ちた行は出ないまま倒した数を 0 に戻し、ほかの行は 1 足す。
    ///   2. ユニークが出なければ、ランク E〜S の「確率 × 倍率」で 1 回振る（合計が 1 を超えたら 1 に縮める）。外れたら何も落とさない。
    /// 倍率はドロップ増加のエンチャントの分（1 + 上乗せ）。天井には効かない。
    /// </summary>
    public static class WeaponDropRoller
    {
        public readonly struct UniqueOption
        {
            /// <summary>1 回倒したときに落とす確率（倍率を掛ける前）。0〜1。</summary>
            public readonly float Chance;

            /// <summary>天井。0 なら無し。</summary>
            public readonly int Pity;

            public UniqueOption(float chance, int pity)
            {
                Chance = chance;
                Pity = pity;
            }
        }

        public enum ResultKind
        {
            None,
            Unique,
            Rank,
        }

        public readonly struct Result
        {
            public readonly ResultKind Kind;

            /// <summary>Kind が Unique のとき、落ちたユニークの行の番号。</summary>
            public readonly int UniqueIndex;

            /// <summary>Kind が Rank のとき、落ちる武器のランク。</summary>
            public readonly WeaponRank Rank;

            private Result(ResultKind kind, int uniqueIndex, WeaponRank rank)
            {
                Kind = kind;
                UniqueIndex = uniqueIndex;
                Rank = rank;
            }

            public static Result None => new Result(ResultKind.None, -1, default);
            public static Result Unique(int index) => new Result(ResultKind.Unique, index, WeaponRank.Unique);
            public static Result OfRank(WeaponRank rank) => new Result(ResultKind.Rank, -1, rank);
        }

        /// <summary>
        /// rankChances は E〜S（<see cref="EnchantmentRankCsv.RankColumns"/> の並び）の確率。
        /// misses は uniques と同じ長さで、行ごとの「出ないまま倒した数」。振った結果で書き換える。
        /// multiplier は確率に掛ける倍率（ドロップ増加）。
        /// </summary>
        public static Result Roll(IReadOnlyList<float> rankChances, IReadOnlyList<UniqueOption> uniques, int[] misses,
            float multiplier, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            int uniqueCount = uniques != null ? uniques.Count : 0;
            if (uniqueCount > 0 && (misses == null || misses.Length < uniqueCount))
            {
                throw new ArgumentException("misses はユニークの行と同じ数だけ要る", nameof(misses));
            }

            double m = Math.Max(0f, multiplier);
            int chosen = RollUnique(uniques, misses, m, random);
            for (int i = 0; i < uniqueCount; i++) misses[i] = i == chosen ? 0 : misses[i] + 1;
            if (chosen >= 0) return Result.Unique(chosen);

            return RollRank(rankChances, m, random);
        }

        private static int RollUnique(IReadOnlyList<UniqueOption> uniques, int[] misses, double multiplier, Random random)
        {
            if (uniques == null) return -1;

            for (int i = 0; i < uniques.Count; i++)
            {
                if (uniques[i].Pity > 0 && misses[i] + 1 >= uniques[i].Pity) return i;
            }

            for (int i = 0; i < uniques.Count; i++)
            {
                double chance = Math.Max(0f, uniques[i].Chance) * multiplier;
                if (chance > 0 && random.NextDouble() < chance) return i;
            }

            return -1;
        }

        private static Result RollRank(IReadOnlyList<float> rankChances, double multiplier, Random random)
        {
            if (rankChances == null) return Result.None;

            WeaponRank[] columns = EnchantmentRankCsv.RankColumns;
            int count = Math.Min(rankChances.Count, columns.Length);
            double total = 0;
            for (int i = 0; i < count; i++) total += Math.Max(0f, rankChances[i]) * multiplier;
            if (total <= 0) return Result.None;

            double scale = total > 1 ? 1 / total : 1;
            double roll = random.NextDouble();
            double cumulative = 0;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                double chance = Math.Max(0f, rankChances[i]) * multiplier * scale;
                if (chance <= 0) continue;

                last = i;
                cumulative += chance;
                if (roll < cumulative) return Result.OfRank(columns[i]);
            }

            // 合計が 1 のときに丸めの誤差で外れないよう、最後のランクに入れる。
            return total >= 1 && last >= 0 ? Result.OfRank(columns[last]) : Result.None;
        }
    }
}
