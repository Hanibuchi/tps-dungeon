using System;
using System.Collections.Generic;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器 1 種類の定義（Notion の武器一覧 DB の 1 行）。名前・説明・絵・拾える物は ItemDefinition から引き継ぐ。
    /// 「強さ」の意味は武器種によって変わる（近接なら DPS）。
    /// 付いたエンチャントは 1 本ごとに違うので、ここではなく ItemInstance が持つ。
    /// ただしユニークは振らず、どの 1 本にも <see cref="FixedEnchantments"/> がそのまま付く。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "TPS Dungeon/Weapons/Weapon")]
    public sealed class WeaponDefinition : ItemDefinition
    {
        [SerializeField] private WeaponRank rank = WeaponRank.E;

        [SerializeField, Min(0f), Tooltip("強さ。近接・遠距離の武器種なら DPS、治癒持続なら毎秒の回復量、召喚ならおとりの体力の元、盾なら防御力。")]
        private float strength;

        [SerializeField] private WeaponTypeDefinition weaponType;

        [SerializeField, Tooltip("手に持ったときの見た目（当たり判定の無いモデル）。未設定なら持たない。")]
        private GameObject heldModel;

        [SerializeField, Tooltip("投擲の武器種で投げたとき、手に持つ見た目が縦に回る速さ（度/秒）。0 なら回らずに先を前へ向けて飛ぶ（投槍）。投擲でなければ使わない。")]
        private float thrownSpinRate;

        [SerializeField, Tooltip("投擲の武器種で投げたとき、壁や床に刺さらずに跳ね返って転がる（石）。投擲でなければ使わない。")]
        private bool thrownBounces;

        [SerializeField, Tooltip("召喚の武器種で呼び出す置物（当たり判定の無い見た目）。召喚でなければ使わない。")]
        private GameObject summonModel;

        [SerializeField, Tooltip("ダメージ軽減の武器種で張る加護の種類（膜の色もこれで決まる）。ダメージ軽減でなければ使わない。")]
        private BlessingKind blessingKind;

        [SerializeField, Tooltip("ユニークに必ず付くエンチャントの種類と段（ユニークは振らない）。正は CSV（Items/Weapons/UniqueEnchantments.csv）で、保存すると取り込まれてここが書き換わる。ユニーク以外では使わない。")]
        private List<FixedEnchantmentEntry> fixedEnchantments = new List<FixedEnchantmentEntry>();

        public WeaponRank Rank => rank;
        public float Strength => strength;
        public WeaponTypeDefinition WeaponType => weaponType;
        public GameObject HeldModel => heldModel;
        public float ThrownSpinRate => thrownSpinRate;
        public bool ThrownBounces => thrownBounces;
        public GameObject SummonModel => summonModel;
        public BlessingKind BlessingKind => blessingKind;
        public IReadOnlyList<FixedEnchantmentEntry> FixedEnchantments => fixedEnchantments;

        /// <summary>エンチャントを振らず、<see cref="FixedEnchantments"/> を付けるか。今はユニークだけ。</summary>
        public bool HasFixedEnchantments => rank == WeaponRank.Unique;

        /// <summary>
        /// 近接武器の数値を出す。characterAttack は持ち主の基礎攻撃力。
        /// crit〜Modifier は永続強化など外から掛かる補正（無ければ null）。
        /// </summary>
        public MeleeWeaponStats ComputeMeleeStats(EnchantmentTotals enchantments, float characterAttack,
            Func<float, float> critChanceModifier = null, Func<float, float> critMultiplierModifier = null)
        {
            var weights = new List<float>();
            var durations = new List<float>();
            if (weaponType != null)
            {
                foreach (MeleeComboStep step in weaponType.ComboSteps)
                {
                    weights.Add(step.damageWeight);
                    durations.Add(step.duration);
                }
            }

            return MeleeWeaponStats.Compute(new MeleeWeaponInputs
            {
                Strength = strength,
                CharacterAttack = characterAttack,
                CharacterAttackWeight = weaponType != null ? weaponType.CharacterAttackWeight : 0f,
                StepWeights = weights,
                StepDurations = durations,
                Enchantments = enchantments,
                BaseCritChance = weaponType != null ? weaponType.BaseCritChance : 0f,
                BaseCritMultiplier = weaponType != null ? weaponType.BaseCritMultiplier : 1f,
                CritChanceModifier = critChanceModifier,
                CritMultiplierModifier = critMultiplierModifier,
            });
        }

        /// <summary>
        /// 遠距離武器（弓・持続弓・杖・投擲・召喚・治癒持続・ダメージ軽減・治癒）の数値を出す。引数は <see cref="ComputeMeleeStats"/> と同じ。
        /// </summary>
        public RangedWeaponStats ComputeRangedStats(EnchantmentTotals enchantments, float characterAttack,
            Func<float, float> critChanceModifier = null, Func<float, float> critMultiplierModifier = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttack = characterAttack,
                CharacterAttackWeight = weaponType != null ? weaponType.CharacterAttackWeight : 0f,
                FireInterval = weaponType != null ? weaponType.FireInterval : 0f,
                RainDuration = weaponType != null ? weaponType.RainDuration : 0f,
                RainTickInterval = weaponType != null ? weaponType.RainTickInterval : 0f,
                FlameDuration = weaponType != null ? weaponType.FlameDuration : 0f,
                FlameTickInterval = weaponType != null ? weaponType.FlameTickInterval : 0f,
                HealDuration = weaponType != null ? weaponType.HealDuration : 0f,
                HealTickInterval = weaponType != null ? weaponType.HealTickInterval : 0f,
                SummonDuration = weaponType != null ? weaponType.SummonDuration : 0f,
                BlessingDuration = weaponType != null ? weaponType.BlessingDuration : 0f,
                BlessingKind = blessingKind,
                SupportTargetCount = weaponType != null ? weaponType.SupportTargetCount : 0,
                BlessingPerStrength = weaponType != null ? weaponType.BlessingPerStrength(blessingKind) : 0f,
                BlessingCap = weaponType != null ? weaponType.BlessingCap(blessingKind) : 0f,
                InstantHeal = weaponType != null && weaponType.IsHeal,
                Enchantments = enchantments,
                BaseCritChance = weaponType != null ? weaponType.BaseCritChance : 0f,
                BaseCritMultiplier = weaponType != null ? weaponType.BaseCritMultiplier : 1f,
                CritChanceModifier = critChanceModifier,
                CritMultiplierModifier = critMultiplierModifier,
            });
        }
    }
}
