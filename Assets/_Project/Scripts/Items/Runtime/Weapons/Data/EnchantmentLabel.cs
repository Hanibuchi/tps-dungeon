using System;
using System.Globalization;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 情報欄に出すエンチャントの 1 行（「ダメージ増加 +15%」「数 +2」など）。UnityEngine に依存しない。
    /// 効果量は同じ種類を全部足した合計（1 個あたり × 個数）で出す。割合は 0.15 で +15%。
    /// </summary>
    public static class EnchantmentLabel
    {
        /// <summary>name の後ろに、kind の読み方で合計の効果量 amount を添える。secondary は副次の値（爆発なら半径 m）。</summary>
        public static string Format(EnchantmentKind kind, string name, float amount, float secondary = 0f)
        {
            string effect = Effect(kind, amount, secondary);
            return string.IsNullOrEmpty(effect) ? name : $"{name} {effect}";
        }

        private static string Effect(EnchantmentKind kind, float amount, float secondary)
        {
            switch (kind)
            {
                case EnchantmentKind.ProjectileCount:
                case EnchantmentKind.Pierce:
                case EnchantmentKind.Multishot:
                    return Signed(amount);
                case EnchantmentKind.ChargeTimeDown:
                    return $"-{Number(amount * 100f)}%";
                case EnchantmentKind.ComboBonus:
                    return $"{Percent(amount)}/段";
                case EnchantmentKind.Knockback:
                    return $"{Signed(amount)} m/s";
                case EnchantmentKind.Regen:
                    return $"{Signed(amount)}/秒";
                case EnchantmentKind.Explosion:
                    return secondary > 0f ? $"{Number(amount * 100f)}%（半径 {Number(secondary)} m）" : $"{Number(amount * 100f)}%";
                case EnchantmentKind.Homing:
                    return string.Empty;
                default:
                    return Percent(amount);
            }
        }

        private static string Percent(float amount) => $"{Signed(amount * 100f)}%";

        private static string Signed(float value) => value < 0f ? $"-{Number(-value)}" : $"+{Number(value)}";

        // 0.15 × 100 が 15.000001 のようになっても 15 と出す。
        private static string Number(float value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }
}
