using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器 1 種類の定義（Notion の武器一覧 DB の 1 行）。名前・説明・絵・拾える物は ItemDefinition から引き継ぐ。
    /// 「強さ」の意味は武器種によって変わる（近接なら DPS）。
    /// 付いたエンチャントは 1 本ごとに違うので、ここではなく ItemInstance が持つ。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "TPS Dungeon/Weapons/Weapon")]
    public sealed class WeaponDefinition : ItemDefinition
    {
        [SerializeField] private WeaponRank rank = WeaponRank.E;

        [SerializeField, Min(0f), Tooltip("強さ。近接の武器種なら DPS。")]
        private float strength;

        [SerializeField] private WeaponTypeDefinition weaponType;

        [SerializeField, Tooltip("手に持ったときの見た目（当たり判定の無いモデル）。未設定なら持たない。")]
        private GameObject heldModel;

        public WeaponRank Rank => rank;
        public float Strength => strength;
        public WeaponTypeDefinition WeaponType => weaponType;
        public GameObject HeldModel => heldModel;

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
    }
}
