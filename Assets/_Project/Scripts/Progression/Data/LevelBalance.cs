using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// レベル・ソウル・経験値の倍率のバランスの式と定数を 1 か所にまとめたもの。UnityEngine に依存しない。
    /// 値はスプレッドシート「バランス」のレベルシートに揃えてある。
    ///
    ///   次までの経験値 need(L)    = ExpFirst × ExpRatioPerLayer^((L − 1) / LevelsPerLayer) を有効数字 2 桁で四捨五入（最大レベルでは 0）
    ///                              1 層（LevelsPerLayer レベル）進むごとに ExpRatioPerLayer 倍になる
    ///   累計経験値(L)            = need(1) + … + need(L)
    ///   全滅したときのソウル soul(L) = 累計経験値(L) ÷ SoulExpDivisor を有効数字 2 桁で四捨五入（到達したレベルの 1 回分）
    ///   経験値の倍率             = ExpMultiplierPerUnlock ^ 解放数（経験値増加の強化。1 つごとに ×2）
    ///
    /// need(L) は Lv82 あたりから int に収まらないので、<see cref="RequiredExp"/> は int.MaxValue で頭打ちにする
    /// （累計とソウルは 64bit で頭打ちにせずに足す）。
    /// </summary>
    public static class LevelBalance
    {
        public const int MinLevel = 1;

        /// <summary>最大レベル。</summary>
        public const int MaxLevel = 100;

        /// <summary>need(1)（Lv1 から Lv2 に要る経験値）。</summary>
        public const double ExpFirst = 100;

        /// <summary>1 層進むごとに必要経験値が何倍になるか。</summary>
        public const double ExpRatioPerLayer = 8;

        /// <summary>1 層あたりのレベル数（1 階あたり 2 レベル × 1 層 5 階）。</summary>
        public const int LevelsPerLayer = 10;

        /// <summary>必要経験値を丸めるときに残す有効数字の桁数。</summary>
        public const int ExpSignificantDigits = 2;

        /// <summary>ソウル ＝ 累計経験値 ÷ この値。</summary>
        public const long SoulExpDivisor = 10;

        /// <summary>ソウルを丸めるときに残す有効数字の桁数。</summary>
        public const int SoulSignificantDigits = 2;

        /// <summary>経験値増加の強化を 1 つ解放するごとに倍率に掛ける量。</summary>
        public const float ExpMultiplierPerUnlock = 2f;

        public static int ClampLevel(int level) => Math.Min(Math.Max(level, MinLevel), MaxLevel);

        /// <summary>丸める前の必要経験値。</summary>
        public static double BaseExp(int level)
        {
            int l = ClampLevel(level);
            return ExpFirst * Math.Pow(ExpRatioPerLayer, (l - 1) / (double)LevelsPerLayer);
        }

        /// <summary>level から level+1 に上がるのに要る経験値 need(L)（丸めたあと。判定にも表示にもこれを使う）。最大レベルでは 0。</summary>
        public static int RequiredExp(int level) => (int)Math.Min(int.MaxValue, RequiredExpLong(level));

        /// <summary>need(L) を頭打ちにせずに返す。最大レベルでは 0。</summary>
        public static long RequiredExpLong(int level)
        {
            if (ClampLevel(level) >= MaxLevel) return 0;
            return Math.Max(1, RoundToSignificant(BaseExp(level), ExpSignificantDigits));
        }

        /// <summary>need(1) から need(level) までの合計。</summary>
        public static long CumulativeExp(int level)
        {
            level = ClampLevel(level);
            long total = 0;
            for (int l = MinLevel; l <= level; l++) total += RequiredExpLong(l);
            return total;
        }

        /// <summary>Lv1・経験値 0 から level に着くまでに要る経験値（need(1) から need(level − 1) までの合計）。</summary>
        public static long TotalExpToReach(int level) => ClampLevel(level) <= MinLevel ? 0 : CumulativeExp(ClampLevel(level) - 1);

        /// <summary>level で全滅したときにもらえるソウル soul(L)。</summary>
        public static long SoulReward(int level)
        {
            if (level < MinLevel) return 0;
            // 10 で割ってから有効数字で丸めても、丸めてから割っても同じ（累計は 100 以上なので割り切れる）。
            return RoundToSignificant(CumulativeExp(level), SoulSignificantDigits) / SoulExpDivisor;
        }

        /// <summary>経験値増加の強化を unlockCount 個解放したときの、経験値の倍率。</summary>
        public static float ExpMultiplier(int unlockCount) => (float)Math.Pow(ExpMultiplierPerUnlock, Math.Max(0, unlockCount));

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

        /// <summary>
        /// 実数を有効数字 digits 桁に四捨五入して整数にする（ちょうど .5 は 0 から遠い側へ。スプレッドシートの ROUND と同じ）。
        /// 例（2 桁）: 123.1 → 120、229.7 → 230。
        /// </summary>
        public static long RoundToSignificant(double value, int digits)
        {
            if (value < 0) return -RoundToSignificant(-value, digits);
            if (value <= 0 || double.IsNaN(value)) return 0;
            int exponent = (int)Math.Floor(Math.Log10(value)) - (digits - 1);
            double unit = Math.Pow(10, exponent);
            // 割り算の誤差で 2.4999999 のようになって切り捨てられないよう、少しだけ持ち上げてから丸める。
            double scaled = value / unit;
            double rounded = Math.Floor(scaled + 0.5 + 1e-9);
            return exponent >= 0 ? (long)Math.Round(rounded * unit) : (long)Math.Round(rounded * unit, MidpointRounding.AwayFromZero);
        }
    }
}
