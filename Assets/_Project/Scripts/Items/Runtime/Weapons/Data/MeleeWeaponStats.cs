using System;
using System.Collections.Generic;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 近接武器の 1 周（コンボ全段）ぶんの計算に要る材料。UnityEngine に依存しない。
    /// </summary>
    public struct MeleeWeaponInputs
    {
        /// <summary>武器の強さ（DPS）。</summary>
        public float Strength;

        /// <summary>持ち主の基礎攻撃力。</summary>
        public float CharacterAttack;

        /// <summary>基礎攻撃力を DPS に足すときの係数。</summary>
        public float CharacterAttackWeight;

        /// <summary>段ごとのダメージの比重。</summary>
        public IReadOnlyList<float> StepWeights;

        /// <summary>段ごとのモーション時間（秒、速射の補正前）。</summary>
        public IReadOnlyList<float> StepDurations;

        public EnchantmentTotals Enchantments;

        /// <summary>補正前のクリティカル率（0〜1）とクリティカル倍率。</summary>
        public float BaseCritChance;
        public float BaseCritMultiplier;

        /// <summary>永続アップグレードなど、外から掛かるクリティカルの補正。</summary>
        public Func<float, float> CritChanceModifier;
        public Func<float, float> CritMultiplierModifier;
    }

    /// <summary>
    /// 近接武器の実際の数値。「強さ」は DPS として定義されているので、1 撃のダメージは 1 周の時間から逆算する。
    ///
    ///   1 周の DPS の元 = 強さ ＋ 基礎攻撃力 × 係数
    ///   段 i の 1 撃     = 元 × 1 周の時間 ÷ Σ比重 × 比重i × (1 ＋ ダメージ増加) × (1 ＋ コンボボーナス × i)
    ///
    /// 1 周の時間は速射の補正前で配るので、速射は時間だけ縮めて DPS を上げる。
    /// 衝撃波（数）・追撃（多重）・爆発は 1 撃に対する割合の上乗せで、ここの DPS には含めない。
    /// エンチャントの効果量は EnchantmentDefinition の値そのもの（例: ダメージ増加 0.15 で +15%）。
    /// </summary>
    public sealed class MeleeWeaponStats
    {
        private readonly int[] hitDamage;
        private readonly float[] stepDurations;

        private MeleeWeaponStats(int[] hitDamage, float[] stepDurations)
        {
            this.hitDamage = hitDamage;
            this.stepDurations = stepDurations;
        }

        public int StepCount => hitDamage.Length;

        /// <summary>攻撃速度の倍率（1 ＋ 速射）。モーションはこの速さで再生する。</summary>
        public float AttackSpeed { get; private set; } = 1f;

        /// <summary>1 周の時間（秒、速射の補正後）。</summary>
        public float CycleDuration { get; private set; }

        public float CritChance { get; private set; }
        public float CritMultiplier { get; private set; } = 1f;

        /// <summary>攻撃判定の大きさの倍率。</summary>
        public float HitboxScale { get; private set; } = 1f;

        /// <summary>ノックバックの上乗せ（m/s）。</summary>
        public float KnockbackBonus { get; private set; }

        /// <summary>スタン・気絶の判定で相対ダメージ量に掛ける倍率。</summary>
        public float ReactionScale { get; private set; } = 1f;

        /// <summary>ヒット時の爆発の威力（ヒットのダメージに対する割合）。0 なら爆発しない。</summary>
        public float ExplosionRatio { get; private set; }

        /// <summary>爆発の半径（m）。</summary>
        public float ExplosionRadius { get; private set; }

        /// <summary>ドロップ率の上乗せ（0.1 で +10%）。ドロップの仕組みが読む。</summary>
        public float DropRateBonus { get; private set; }

        /// <summary>走る段（ダッシュ突き）の走る時間の倍率（1 ＋ 持続時間）。速さは同じなので距離も同じだけ伸びる。</summary>
        public float LungeTimeScale { get; private set; } = 1f;

        /// <summary>叩きつけから扇状に走る衝撃波の本数（「数」の合計の切り捨て）。</summary>
        public int ShockwaveCount { get; private set; }

        /// <summary>叩きつけのあと前へずらして落とす追撃の回数（「多重」の合計の切り捨て）。</summary>
        public int FollowUpCount { get; private set; }

        /// <summary>段 step（0 始まり）の 1 撃（クリティカル前）。</summary>
        public int HitDamage(int step) => step >= 0 && step < hitDamage.Length ? hitDamage[step] : 0;

        /// <summary>段 step のモーション時間（秒、速射の補正後）。</summary>
        public float StepDuration(int step) => step >= 0 && step < stepDurations.Length ? stepDurations[step] : 0f;

        /// <summary>クリティカル込みの平均 DPS（爆発は含めない）。表示用。</summary>
        public float AverageDps
        {
            get
            {
                if (CycleDuration <= 0f) return 0f;
                long sum = 0;
                foreach (int d in hitDamage) sum += d;
                float critFactor = 1f + CritChance * (CritMultiplier - 1f);
                return sum * critFactor / CycleDuration;
            }
        }

        /// <summary>クリティカルを掛けた 1 撃。四捨五入、最低 1。</summary>
        public int ApplyCritical(int damage) => Math.Max(1, RoundToInt(damage * CritMultiplier));

        public static MeleeWeaponStats Compute(MeleeWeaponInputs inputs)
        {
            IReadOnlyList<float> weights = inputs.StepWeights ?? Array.Empty<float>();
            IReadOnlyList<float> durations = inputs.StepDurations ?? Array.Empty<float>();
            int count = Math.Min(weights.Count, durations.Count);
            EnchantmentTotals enchant = inputs.Enchantments ?? EnchantmentTotals.Empty;

            float baseCycle = 0f;
            float weightSum = 0f;
            for (int i = 0; i < count; i++)
            {
                baseCycle += Math.Max(0f, durations[i]);
                weightSum += Math.Max(0f, weights[i]);
            }

            float speed = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.RapidFire));
            float source = Math.Max(0f, inputs.Strength + inputs.CharacterAttack * inputs.CharacterAttackWeight);
            float damageUp = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.DamageUp));
            float comboBonus = enchant.Amount(EnchantmentKind.ComboBonus);

            var hits = new int[count];
            var scaledDurations = new float[count];
            for (int i = 0; i < count; i++)
            {
                float share = weightSum > 0f ? Math.Max(0f, weights[i]) / weightSum : 0f;
                double hit = source * baseCycle * share * damageUp * (1.0 + comboBonus * i);
                hits[i] = Math.Max(1, RoundToInt(hit));
                scaledDurations[i] = Math.Max(0f, durations[i]) / speed;
            }

            float critChance = inputs.BaseCritChance + enchant.Amount(EnchantmentKind.CritChance);
            if (inputs.CritChanceModifier != null) critChance = inputs.CritChanceModifier(critChance);
            float critMultiplier = inputs.BaseCritMultiplier;
            if (inputs.CritMultiplierModifier != null) critMultiplier = inputs.CritMultiplierModifier(critMultiplier);

            return new MeleeWeaponStats(hits, scaledDurations)
            {
                AttackSpeed = speed,
                CycleDuration = baseCycle / speed,
                CritChance = Clamp01(critChance),
                CritMultiplier = Math.Max(1f, critMultiplier),
                HitboxScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.Size)),
                KnockbackBonus = Math.Max(0f, enchant.Amount(EnchantmentKind.Knockback)),
                ReactionScale = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.Stun)),
                ExplosionRatio = Math.Max(0f, enchant.Amount(EnchantmentKind.Explosion)),
                ExplosionRadius = Math.Max(0f, enchant.Secondary(EnchantmentKind.Explosion)),
                DropRateBonus = Math.Max(0f, enchant.Amount(EnchantmentKind.DropUp)),
                LungeTimeScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.Duration)),
                ShockwaveCount = FloorCount(enchant.Amount(EnchantmentKind.ProjectileCount)),
                FollowUpCount = FloorCount(enchant.Amount(EnchantmentKind.Multishot)),
            };
        }

        /// <summary>1 撃 hit の ratio 倍（衝撃波・追撃）。四捨五入、ratio と hit が正なら最低 1。</summary>
        public static int Share(int hit, float ratio) => ratio > 0f && hit > 0 ? Math.Max(1, RoundToInt(hit * (double)ratio)) : 0;

        /// <summary>1 撃 hit に添える爆発のダメージ。0 なら爆発しない。</summary>
        public int ExplosionDamage(int hit) =>
            ExplosionRatio > 0f && ExplosionRadius > 0f && hit > 0 ? Math.Max(1, RoundToInt(hit * ExplosionRatio)) : 0;

        private static int RoundToInt(double value)
        {
            double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded >= int.MaxValue) return int.MaxValue;
            if (rounded <= int.MinValue) return int.MinValue;
            return (int)rounded;
        }

        // 1.0 が浮動小数の足し算で 0.9999… になっても 1 と数える。
        private static int FloorCount(float value) => value <= 0f ? 0 : (int)Math.Floor(value + 1e-4);

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
