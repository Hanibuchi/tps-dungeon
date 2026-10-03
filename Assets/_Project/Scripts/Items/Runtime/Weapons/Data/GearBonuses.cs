using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>ホットバーの 1 枠のうち、お守り・盾の計算に要るところ。お守り・盾でない枠は Gear が None。</summary>
    public readonly struct GearSlot
    {
        public GearSlot(PassiveGear gear, float strength, EnchantmentTotals enchantments)
        {
            Gear = gear;
            Strength = strength;
            Enchantments = enchantments;
        }

        public PassiveGear Gear { get; }

        /// <summary>武器の強さ。盾なら防御力。お守りでは使わない。</summary>
        public float Strength { get; }

        public EnchantmentTotals Enchantments { get; }
    }

    /// <summary>
    /// ホットバーのお守り・盾から出る効果の合計。UnityEngine に依存しない。
    /// お守りはホットバーにある分を全部足す。盾は片手武器を持っている間だけ、防御のいちばん高い 1 枚が効く（そのエンチャントもその 1 枚分）。
    /// クリティカル率・ドロップ増加・数・多重は手の武器のエンチャントに足し（<see cref="WeaponTotals"/>）、
    /// 体力・移動速度・経験値・自然回復・防御は持ち主に掛かる。
    /// </summary>
    public sealed class GearBonuses
    {
        /// <summary>受けるダメージ × 100 / (100 + 防御) の 100。</summary>
        public const float DefenseScale = 100f;

        /// <summary>防御力のエンチャントで減らせる割合の上限。</summary>
        public const float MaxDefenseEnchant = 0.8f;

        private static readonly EnchantmentKind[] WeaponKinds =
        {
            EnchantmentKind.CritChance,
            EnchantmentKind.DropUp,
            EnchantmentKind.ProjectileCount,
            EnchantmentKind.Multishot,
        };

        private GearBonuses(EnchantmentTotals totals, int activeShieldIndex, float shieldDefense)
        {
            Totals = totals;
            ActiveShieldIndex = activeShieldIndex;
            ShieldDefense = shieldDefense;

            WeaponTotals = new EnchantmentTotals();
            foreach (EnchantmentKind kind in WeaponKinds) WeaponTotals.AddKind(totals, kind);
        }

        /// <summary>何も無いとき。</summary>
        public static GearBonuses None { get; } = new GearBonuses(new EnchantmentTotals(), -1, 0f);

        /// <summary>効いているお守り・盾のエンチャントの合計。</summary>
        public EnchantmentTotals Totals { get; }

        /// <summary>手の武器のエンチャントに足す分（クリティカル率・ドロップ増加・数・多重）。</summary>
        public EnchantmentTotals WeaponTotals { get; }

        /// <summary>効いている盾の枠。無ければ -1。</summary>
        public int ActiveShieldIndex { get; }

        /// <summary>効いている盾の防御力。無ければ 0。</summary>
        public float ShieldDefense { get; }

        /// <summary>最大 HP の上乗せ（0.1 で +10%）。</summary>
        public float MaxHpPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.MaxHp));

        /// <summary>歩く・走る速さの上乗せ（0.1 で +10%）。</summary>
        public float MoveSpeedPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.MoveSpeed));

        /// <summary>得られる経験値の上乗せ（0.1 で +10%）。</summary>
        public float ExpPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.Exp));

        /// <summary>毎秒の回復量。</summary>
        public float RegenPerSecond => Math.Max(0f, Totals.Amount(EnchantmentKind.Regen));

        /// <summary>受けるダメージに掛ける倍率（1 で減らない）。</summary>
        public float DamageTaken => DamageTakenFor(ShieldDefense, Totals.Amount(EnchantmentKind.Defense));

        /// <summary>防御 defense と防御力のエンチャントの合計 defenseEnchant から、受けるダメージの倍率を出す。</summary>
        public static float DamageTakenFor(float defense, float defenseEnchant)
        {
            float shield = DefenseScale / (DefenseScale + Math.Max(0f, defense));
            float enchant = 1f - Math.Min(MaxDefenseEnchant, Math.Max(0f, defenseEnchant));
            return shield * enchant;
        }

        /// <summary>
        /// slots はホットバーの枠の並び。holdingShieldWeapon は今選んでいる物が片手武器（盾が効く）か。
        /// </summary>
        public static GearBonuses Compute(IReadOnlyList<GearSlot> slots, bool holdingShieldWeapon)
        {
            if (slots == null || slots.Count == 0) return None;

            var totals = new EnchantmentTotals();
            int shieldIndex = -1;
            float shieldDefense = 0f;
            for (int i = 0; i < slots.Count; i++)
            {
                GearSlot slot = slots[i];
                if (slot.Gear == PassiveGear.Charm) totals.AddAll(slot.Enchantments);
                else if (slot.Gear == PassiveGear.Shield && holdingShieldWeapon && (shieldIndex < 0 || slot.Strength > shieldDefense))
                {
                    shieldIndex = i;
                    shieldDefense = slot.Strength;
                }
            }

            if (shieldIndex >= 0) totals.AddAll(slots[shieldIndex].Enchantments);
            return new GearBonuses(totals, shieldIndex, Math.Max(0f, shieldDefense));
        }
    }
}
