using System;
using System.Collections.Generic;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 魂の強化の効果の種類。強化の表（SoulUpgrades.csv）の effect 列に書く名前と対になる。
    /// 同じ効果のノードを何段も買えば値（value）は足し合わさる（<see cref="SoulUpgradeTree.Total"/>）。
    /// </summary>
    public enum SoulUpgradeEffect
    {
        None = 0,
        /// <summary>得る経験値の倍率に足す（0.1 で +10%）。</summary>
        ExpRate,
        /// <summary>仲間の人数を増やす（人）。</summary>
        PartySize,
        /// <summary>転生で持ち越せる武器の数（本）。</summary>
        CarryOver,
        /// <summary>共有のバッグの枠を増やす（枠）。</summary>
        BagSize,
        /// <summary>クリティカルの倍率に足す（0.25 で ×1.5 が ×1.75）。</summary>
        CritMultiplier,
        /// <summary>クリティカル率に足す（0.03 で +3%）。</summary>
        CritChance,
        /// <summary>武器のドロップ率の上乗せ（0.1 で +10%）。</summary>
        DropRate,
        /// <summary>ユニーク武器のドロップ率の上乗せ（0.5 で +50%）。</summary>
        UniqueDropRate,
        /// <summary>地上の武器屋を開ける。</summary>
        WeaponShop,
        /// <summary>地上の銀行を開ける。</summary>
        Bank,
        /// <summary>その層（value。1 始まり）のボス階へのショートカットを開ける。</summary>
        Shortcut,
        /// <summary>武器屋に並ぶ武器の数を増やす（本）。</summary>
        ShopStock,
        /// <summary>武器の売値の上乗せ（0.2 で +20%）。</summary>
        SellPrice,
        /// <summary>武器図鑑を足す。</summary>
        WeaponCodex,
        /// <summary>敵図鑑を足す。</summary>
        EnemyCodex,
        /// <summary>最大体力の上乗せ（0.1 で +10%）。</summary>
        MaxHp,
        /// <summary>レベルが上がってからしばらく経験値が増える時間（秒）。</summary>
        ExpBoost,
    }

    /// <summary>効果の名前（CSV の effect 列）と種類の対応。</summary>
    public static class SoulUpgradeEffects
    {
        private static readonly Dictionary<string, SoulUpgradeEffect> ByName = new Dictionary<string, SoulUpgradeEffect>(StringComparer.OrdinalIgnoreCase)
        {
            { "exp_rate", SoulUpgradeEffect.ExpRate },
            { "party_size", SoulUpgradeEffect.PartySize },
            { "carry_over", SoulUpgradeEffect.CarryOver },
            { "bag_size", SoulUpgradeEffect.BagSize },
            { "crit_multiplier", SoulUpgradeEffect.CritMultiplier },
            { "crit_chance", SoulUpgradeEffect.CritChance },
            { "drop_rate", SoulUpgradeEffect.DropRate },
            { "unique_drop_rate", SoulUpgradeEffect.UniqueDropRate },
            { "weapon_shop", SoulUpgradeEffect.WeaponShop },
            { "bank", SoulUpgradeEffect.Bank },
            { "shortcut", SoulUpgradeEffect.Shortcut },
            { "shop_stock", SoulUpgradeEffect.ShopStock },
            { "sell_price", SoulUpgradeEffect.SellPrice },
            { "weapon_codex", SoulUpgradeEffect.WeaponCodex },
            { "enemy_codex", SoulUpgradeEffect.EnemyCodex },
            { "max_hp", SoulUpgradeEffect.MaxHp },
            { "exp_boost", SoulUpgradeEffect.ExpBoost },
        };

        public static bool TryParse(string name, out SoulUpgradeEffect effect)
        {
            effect = SoulUpgradeEffect.None;
            return !string.IsNullOrEmpty(name) && ByName.TryGetValue(name.Trim(), out effect);
        }

        public static IEnumerable<string> Names => ByName.Keys;

        /// <summary>
        /// 値を画面に出す形にする（「経験値 +10%」など）。解放するだけの効果は効果の名前だけ。
        /// </summary>
        public static string Describe(SoulUpgradeEffect effect, float value)
        {
            switch (effect)
            {
                case SoulUpgradeEffect.ExpRate: return $"経験値 +{Percent(value)}";
                case SoulUpgradeEffect.PartySize: return $"仲間 +{value:0} 人";
                case SoulUpgradeEffect.CarryOver: return $"持ち越せる武器 +{value:0} 本";
                case SoulUpgradeEffect.BagSize: return $"バッグ +{value:0} 枠";
                case SoulUpgradeEffect.CritMultiplier: return $"クリティカル倍率 +{value:0.##}";
                case SoulUpgradeEffect.CritChance: return $"クリティカル率 +{Percent(value)}";
                case SoulUpgradeEffect.DropRate: return $"ドロップ率 +{Percent(value)}";
                case SoulUpgradeEffect.UniqueDropRate: return $"ユニークのドロップ率 +{Percent(value)}";
                case SoulUpgradeEffect.WeaponShop: return "武器屋が開く";
                case SoulUpgradeEffect.Bank: return "銀行が開く";
                case SoulUpgradeEffect.Shortcut: return $"第 {value:0} 層へのショートカット";
                case SoulUpgradeEffect.ShopStock: return $"武器屋の品数 +{value:0}";
                case SoulUpgradeEffect.SellPrice: return $"売値 +{Percent(value)}";
                case SoulUpgradeEffect.WeaponCodex: return "武器図鑑";
                case SoulUpgradeEffect.EnemyCodex: return "敵図鑑";
                case SoulUpgradeEffect.MaxHp: return $"最大体力 +{Percent(value)}";
                case SoulUpgradeEffect.ExpBoost: return $"レベルアップ後 {Duration(value)} 経験値が増える";
                default: return string.Empty;
            }
        }

        private static string Percent(float value) => $"{Math.Round(value * 100f, 1):0.#}%";

        public static string Duration(float seconds)
        {
            int total = (int)Math.Round(seconds);
            int minutes = total / 60;
            int rest = total % 60;
            if (minutes == 0) return $"{rest} 秒";
            return rest == 0 ? $"{minutes} 分" : $"{minutes} 分 {rest} 秒";
        }
    }
}
