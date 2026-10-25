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
        /// <summary>階の部屋（敵の出る部屋）を全部攻略したときにもらえる経験値（倍率を掛けない固定の量）。</summary>
        FloorClearExp,
        /// <summary>壊した箱（木箱・宝箱）から武器が出る重みの上乗せ（0.5 で +50%）。</summary>
        BoxWeaponRate,
        /// <summary>階に置く木箱の数・宝箱の確率の上乗せ（0.25 で +25%）。</summary>
        BoxSpawnRate,
        /// <summary>ガチャ屋の台（value。1 で E〜B、2 で D〜A、3 で B〜S）。1 つでも解放すればガチャ屋が開く。</summary>
        Gacha,
        /// <summary>倉庫の枠を増やす（枠）。</summary>
        BankSize,
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
            { "floor_clear_exp", SoulUpgradeEffect.FloorClearExp },
            { "box_weapon", SoulUpgradeEffect.BoxWeaponRate },
            { "box_count", SoulUpgradeEffect.BoxSpawnRate },
            { "gacha", SoulUpgradeEffect.Gacha },
            { "bank_size", SoulUpgradeEffect.BankSize },
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
                case SoulUpgradeEffect.Bank: return "倉庫が開く";
                case SoulUpgradeEffect.Shortcut: return $"第 {value:0} 層へのショートカット";
                case SoulUpgradeEffect.ShopStock: return $"武器屋の品数 +{value:0}";
                case SoulUpgradeEffect.SellPrice: return $"売値 +{Percent(value)}";
                case SoulUpgradeEffect.WeaponCodex: return "武器図鑑";
                case SoulUpgradeEffect.EnemyCodex: return "敵図鑑";
                case SoulUpgradeEffect.MaxHp: return $"最大体力 +{Percent(value)}";
                case SoulUpgradeEffect.FloorClearExp: return $"全部屋攻略で経験値 +{value:0}";
                case SoulUpgradeEffect.BoxWeaponRate: return $"箱から武器 +{Percent(value)}";
                case SoulUpgradeEffect.BoxSpawnRate: return $"箱の数 +{Percent(value)}";
                case SoulUpgradeEffect.Gacha: return $"ガチャ（{GachaRange(value)}）";
                case SoulUpgradeEffect.BankSize: return $"倉庫 +{value:0} 枠";
                default: return string.Empty;
            }
        }

        /// <summary>ガチャの台（1〜3）が出す武器のランクの幅。</summary>
        public static string GachaRange(float tier)
        {
            switch ((int)Math.Round(tier))
            {
                case 1: return "E〜B";
                case 2: return "D〜A";
                case 3: return "B〜S";
                default: return "?";
            }
        }

        private static string Percent(float value) => $"{Math.Round(value * 100f, 1):0.#}%";
    }
}
