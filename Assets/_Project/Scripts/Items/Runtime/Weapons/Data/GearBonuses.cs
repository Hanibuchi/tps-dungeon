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
    /// お守りはホットバーにある分を全部足す。盾は手に持っている分だけ効く。
    /// - 盾そのものを選んでいれば、その盾（メインハンド）。
    /// - 選んでいる物が盾を持てる武器種（canUseShield）なら、ほかの盾のうち防御のいちばん高い 1 枚（オフハンド）。
    /// 2 枚とも持っていれば防御もエンチャントも 2 枚分を足す。
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

        private GearBonuses(EnchantmentTotals totals, int mainShieldIndex, int offHandShieldIndex, float shieldDefense)
        {
            Totals = totals;
            MainShieldIndex = mainShieldIndex;
            OffHandShieldIndex = offHandShieldIndex;
            ShieldDefense = shieldDefense;

            WeaponTotals = new EnchantmentTotals();
            foreach (EnchantmentKind kind in WeaponKinds) WeaponTotals.AddKind(totals, kind);
        }

        /// <summary>何も無いとき。</summary>
        public static GearBonuses None { get; } = new GearBonuses(new EnchantmentTotals(), -1, -1, 0f);

        /// <summary>効いているお守り・盾のエンチャントの合計。</summary>
        public EnchantmentTotals Totals { get; }

        /// <summary>手の武器のエンチャントに足す分（クリティカル率・ドロップ増加・数・多重）。</summary>
        public EnchantmentTotals WeaponTotals { get; }

        /// <summary>選んでいて利き手に持っている盾の枠。無ければ -1。</summary>
        public int MainShieldIndex { get; }

        /// <summary>オフハンド（左手）に持っている盾の枠。無ければ -1。</summary>
        public int OffHandShieldIndex { get; }

        /// <summary>効いている盾の防御力の合計。無ければ 0。</summary>
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
        /// slots はホットバーの枠の並び。offHandShield は今選んでいる物が盾を持てる武器種（canUseShield）か。
        /// selectedIndex は今選んでいる枠（盾ならその盾が利き手で効く）。選んでいる枠を問わないなら -1。
        /// </summary>
        public static GearBonuses Compute(IReadOnlyList<GearSlot> slots, bool offHandShield, int selectedIndex = -1)
        {
            if (slots == null || slots.Count == 0) return None;

            var totals = new EnchantmentTotals();
            int mainIndex = selectedIndex >= 0 && selectedIndex < slots.Count && slots[selectedIndex].Gear == PassiveGear.Shield ? selectedIndex : -1;
            int offHandIndex = -1;
            float offHandDefense = 0f;
            for (int i = 0; i < slots.Count; i++)
            {
                GearSlot slot = slots[i];
                if (slot.Gear == PassiveGear.Charm) totals.AddAll(slot.Enchantments);
                else if (slot.Gear == PassiveGear.Shield && offHandShield && i != mainIndex && (offHandIndex < 0 || slot.Strength > offHandDefense))
                {
                    offHandIndex = i;
                    offHandDefense = slot.Strength;
                }
            }

            float defense = 0f;
            if (mainIndex >= 0)
            {
                totals.AddAll(slots[mainIndex].Enchantments);
                defense += Math.Max(0f, slots[mainIndex].Strength);
            }
            if (offHandIndex >= 0)
            {
                totals.AddAll(slots[offHandIndex].Enchantments);
                defense += Math.Max(0f, offHandDefense);
            }

            return new GearBonuses(totals, mainIndex, offHandIndex, defense);
        }
    }
}
