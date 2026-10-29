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

        /// <summary>盾の防御力。強さに (1 ＋ 防御力のエンチャント) を掛ける（0.1 で +10%）。</summary>
        public float Defense => Math.Max(0f, Strength) * (1f + Math.Max(0f, Enchantments != null ? Enchantments.Amount(EnchantmentKind.Defense) : 0f));
    }

    /// <summary>
    /// ホットバーのお守り・盾から出る効果の合計。UnityEngine に依存しない。
    /// お守りはホットバーにある分を全部足す。盾は手に持っている分だけ効く。
    /// - 盾そのものを選んでいれば、その盾（メインハンド）。
    /// - 選んでいる物が盾を持てる武器種（canUseShield）なら、ほかの盾のうち防御のいちばん高い 1 枚（オフハンド）。
    /// 2 枚とも持っていれば防御もエンチャントも 2 枚分を足す。防御力のエンチャントはその盾の防御力を割合で上げる（<see cref="GearSlot.Defense"/>）。
    /// クリティカル率・ドロップ増加・数・多重は手の武器のエンチャントに足し（<see cref="WeaponTotals"/>）、
    /// 体力・移動速度・経験値・自然回復・防御は持ち主に掛かる。
    /// </summary>
    public sealed class GearBonuses
    {
        /// <summary>受けるダメージ × 100 / (100 + 防御) の 100。</summary>
        public const float DefenseScale = 100f;

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

        /// <summary>効いている盾の防御力（防御力のエンチャントの分も）の合計。無ければ 0。</summary>
        public float ShieldDefense { get; }

        /// <summary>最大 HP の上乗せ（0.1 で +10%）。</summary>
        public float MaxHpPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.MaxHp));

        /// <summary>歩く・走る速さの上乗せ（0.1 で +10%）。</summary>
        public float MoveSpeedPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.MoveSpeed));

        /// <summary>得られる経験値の上乗せ（0.1 で +10%）。</summary>
        public float ExpPercent => Math.Max(0f, Totals.Amount(EnchantmentKind.Exp));

        /// <summary>毎秒の回復量。</summary>
        public float RegenPerSecond => Math.Max(0f, Totals.Amount(EnchantmentKind.Regen));

        /// <summary>自分の盾だけで受けるダメージに掛ける倍率（1 で減らない）。実際は両隣の盾も足す（<see cref="WithNeighbors"/>）。</summary>
        public float DamageTaken => DamageTakenFor(ShieldDefense);

        /// <summary>
        /// 並びの index 番目のキャラの防御力。自分と両隣（index − 1, index + 1）の盾の防御力 defenses を足す。
        /// 並びの外（端の外側）は数えない。
        /// </summary>
        public static float WithNeighbors(IReadOnlyList<float> defenses, int index)
        {
            if (defenses == null || index < 0 || index >= defenses.Count) return 0f;

            float sum = 0f;
            for (int i = Math.Max(0, index - 1); i <= Math.Min(defenses.Count - 1, index + 1); i++) sum += Math.Max(0f, defenses[i]);
            return sum;
        }

        /// <summary>防御 defense から、受けるダメージの倍率を出す。</summary>
        public static float DamageTakenFor(float defense) => DefenseScale / (DefenseScale + Math.Max(0f, defense));

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
                else if (slot.Gear == PassiveGear.Shield && offHandShield && i != mainIndex && (offHandIndex < 0 || slot.Defense > offHandDefense))
                {
                    offHandIndex = i;
                    offHandDefense = slot.Defense;
                }
            }

            float defense = 0f;
            if (mainIndex >= 0)
            {
                totals.AddAll(slots[mainIndex].Enchantments);
                defense += slots[mainIndex].Defense;
            }
            if (offHandIndex >= 0)
            {
                totals.AddAll(slots[offHandIndex].Enchantments);
                defense += offHandDefense;
            }

            return new GearBonuses(totals, mainIndex, offHandIndex, defense);
        }
    }
}
