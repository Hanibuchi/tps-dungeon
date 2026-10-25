using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// レベル・ソウル・経験値の倍率のバランスの式と定数を 1 か所にまとめたもの。UnityEngine に依存しない。
    ///
    ///   元の値 base(L)           = ExpCoefficient × L³ + ExpConstant
    ///   次までの経験値 need(L)    = base(L) を有効数字 2 桁で四捨五入（10〜99 はそのまま。最大レベルでは 0）
    ///   累計経験値(L)            = need(1) + … + need(L)
    ///   全滅したときのソウル soul(L) = SoulCoefficient × L^SoulExponent（到達したレベルの 1 回分）
    ///   経験値の倍率             = 1 + ExpBonusPerUnlock × 解放数（経験値増加の強化）
    ///
    /// 丸めは整数で計算する（浮動小数点の誤差で .5 の四捨五入がずれないように）。ソウルは 64bit で持つ（Lv100 で 10 億）。
    /// </summary>
    public static class LevelBalance
    {
        public const int MinLevel = 1;

        /// <summary>最大レベル。</summary>
        public const int MaxLevel = 100;

        /// <summary>必要経験値の元の値の L³ に掛ける係数。</summary>
        public const long ExpCoefficient = 1;

        /// <summary>必要経験値の元の値に足す固定値。</summary>
        public const long ExpConstant = 100;

        /// <summary>必要経験値を丸めるときに残す有効数字の桁数。</summary>
        public const int ExpSignificantDigits = 2;

        /// <summary>ソウルの係数。</summary>
        public const long SoulCoefficient = 10;

        /// <summary>ソウルの指数（レベルの何乗か）。</summary>
        public const int SoulExponent = 4;

        /// <summary>経験値増加の強化を 1 つ解放するごとに倍率に足す量。</summary>
        public const float ExpBonusPerUnlock = 1f;

        public static int ClampLevel(int level) => Math.Min(Math.Max(level, MinLevel), MaxLevel);

        /// <summary>丸める前の必要経験値 base(L)。</summary>
        public static long BaseExp(int level)
        {
            long l = ClampLevel(level);
            return ExpCoefficient * l * l * l + ExpConstant;
        }

        /// <summary>level から level+1 に上がるのに要る経験値 need(L)（丸めたあと。判定にも表示にもこれを使う）。最大レベルでは 0。</summary>
        public static int RequiredExp(int level)
        {
            if (ClampLevel(level) >= MaxLevel) return 0;
            long need = RoundToSignificant(BaseExp(level), ExpSignificantDigits);
            return (int)Math.Min(int.MaxValue, Math.Max(1, need));
        }

        /// <summary>need(1) から need(level) までの合計。</summary>
        public static long CumulativeExp(int level)
        {
            level = ClampLevel(level);
            long total = 0;
            for (int l = MinLevel; l <= level; l++) total += RequiredExp(l);
            return total;
        }

        /// <summary>Lv1・経験値 0 から level に着くまでに要る経験値（need(1) から need(level − 1) までの合計）。</summary>
        public static long TotalExpToReach(int level) => ClampLevel(level) <= MinLevel ? 0 : CumulativeExp(ClampLevel(level) - 1);

        /// <summary>level で全滅したときにもらえるソウル soul(L)。</summary>
        public static long SoulReward(int level)
        {
            if (level < MinLevel) return 0;
            long souls = SoulCoefficient;
            for (int i = 0; i < SoulExponent; i++) souls *= ClampLevel(level);
            return souls;
        }

        /// <summary>経験値増加の強化を unlockCount 個解放したときの、経験値の倍率。</summary>
        public static float ExpMultiplier(int unlockCount) => 1f + ExpBonusPerUnlock * Math.Max(0, unlockCount);

        /// <summary>
        /// 有効数字 digits 桁に四捨五入する（整数だけで計算する）。digits 桁以下の値はそのまま。
        /// 例（2 桁）: 101 → 100、108 → 110、225 → 230、27,100 → 27,000、125,100 → 130,000。
        /// </summary>
        public static long RoundToSignificant(long value, int digits)
        {
            if (value < 0) return -RoundToSignificant(-value, digits);
            long limit = 1;
            for (int i = 0; i < digits; i++) limit *= 10;
            if (value < limit) return value;

            long unit = 1;
            while (value / unit >= limit) unit *= 10;
            return (value + unit / 2) / unit * unit;
        }
    }
}
