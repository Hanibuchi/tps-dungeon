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

        [SerializeField, Min(0), Tooltip("売値のうち、この武器だけの金額。ランクとエンチャントのランクの金額（Settings/Tables/SellPrices.csv）を足したものが売値。正は CSV（Weapons.csv の price）。")]
        private int basePrice;

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

        [SerializeField, Tooltip("ユニークに必ず付くエンチャントの種類と段（ユニークは振らない）。正は CSV（Settings/Tables/UniqueEnchantments.csv）で、保存すると取り込まれてここが書き換わる。ユニーク以外では使わない。")]
        private List<FixedEnchantmentEntry> fixedEnchantments = new List<FixedEnchantmentEntry>();

        public WeaponRank Rank => rank;
        public float Strength => strength;
        public WeaponTypeDefinition WeaponType => weaponType;
        public int BasePrice => basePrice;
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
                ComboCooldown = weaponType != null ? weaponType.ComboCooldown : 0f,
                Enchantments = enchantments,
                BaseCritChance = weaponType != null ? weaponType.BaseCritChance : 0f,
                BaseCritMultiplier = weaponType != null ? weaponType.BaseCritMultiplier : 1f,
                CritChanceModifier = critChanceModifier,
                CritMultiplierModifier = critMultiplierModifier,
            });
        }

        /// <summary>
        /// 鑑定で出す、エンチャント enchantments 込みの実効 DPS（持ち主の基礎攻撃力・お守り・永続強化は入れない）。
        /// 近接は走る段が持続時間と多重で延びる分も 1 周に入れる（MeleeAttacker の段の時間と同じ）。敵を傷つけない武器種では 0。
        /// </summary>
        public float EffectiveDps(EnchantmentTotals enchantments)
        {
            if (weaponType == null) return 0f;
            if (weaponType.IsMelee)
            {
                MeleeWeaponStats stats = ComputeMeleeStats(enchantments, 0f);
                var motions = new List<MeleeStepMotion>();
                float extra = 0f;
                foreach (MeleeComboStep step in weaponType.ComboSteps)
                {
                    motions.Add(step.motion);
                    if (step.motion != MeleeStepMotion.Lunge) continue;
                    float lunge = Mathf.Max(0f, step.lungeDuration) * stats.LungeTimeScale;
                    extra += Mathf.Max(0f, step.lungeDuration * (stats.LungeTimeScale - 1f)) + stats.FollowUpCount * (Mathf.Max(0f, step.hitTime) + lunge);
                }

                return stats.EffectiveDps(motions, extra);
            }

            return weaponType.IsRanged ? ComputeRangedStats(enchantments, 0f).EffectiveDps : 0f;
        }

        /// <summary>鑑定で出す、エンチャント enchantments 込みの毎秒の回復量（治癒・治癒持続）。回復しない武器種では 0。</summary>
        public float HealPerSecond(EnchantmentTotals enchantments) =>
            weaponType != null && weaponType.IsRanged ? ComputeRangedStats(enchantments, 0f).HealPerSecond : 0f;

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
