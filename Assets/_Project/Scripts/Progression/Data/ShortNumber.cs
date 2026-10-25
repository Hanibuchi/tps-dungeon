using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 大きな数を短く出す（ソウル・強化のコスト・経験値など）。UnityEngine に依存しない。
    /// 1,000 未満はそのまま、それより上は K（千）・M（百万）・B（十億）・T（兆）を付けて有効数字 3 桁まで出す。
    /// 例: 950 → 950、2,000 → 2K、25,000 → 25K、1,234,567 → 1.23M、3,000,000,000 → 3B。
    /// 端数は切り捨てる（999,999 を 1M と出して、足りないのに足りるように見せないため）。
    /// </summary>
    public static class ShortNumber
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };

        public static string Format(long value)
        {
            if (value < 0) return "-" + Format(value == long.MinValue ? long.MaxValue : -value);
            if (value < 1000) return value.ToString();

            int tier = 0;
            long unit = 1;
            while (tier < Suffixes.Length - 1 && value / unit >= 1000)
            {
                unit *= 1000;
                tier++;
            }

            long whole = value / unit;
            // 有効数字 3 桁に入る小数の桁数（1.23K / 12.3K / 123K）。
            int decimals = whole >= 100 ? 0 : whole >= 10 ? 1 : 2;
            long scale = decimals == 2 ? 100 : decimals == 1 ? 10 : 1;
            long fraction = value % unit / (unit / scale);

            string text = whole.ToString();
            if (fraction > 0)
            {
                string digits = fraction.ToString().PadLeft(decimals, '0').TrimEnd('0');
                text += "." + digits;
            }
            return text + Suffixes[tier];
        }
    }
}
