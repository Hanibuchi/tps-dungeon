using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 死んだときのレベルを、いくつのソウルに変えるか。UnityEngine に依存しない。
    /// 1 人分は round(perLevel × (Lv − 1)^exponent)。Lv1 のままなら 0。パーティーの分は全員の足し算。
    /// </summary>
    public static class SoulConversion
    {
        public const float DefaultPerLevel = 2f;
        public const float DefaultExponent = 1.3f;

        public static int SoulsFor(int level, float perLevel = DefaultPerLevel, float exponent = DefaultExponent)
        {
            int gained = level - GrowthCurve.MinLevel;
            if (gained <= 0 || perLevel <= 0f) return 0;
            double value = Math.Round(perLevel * Math.Pow(gained, Math.Max(0f, exponent)), MidpointRounding.AwayFromZero);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
