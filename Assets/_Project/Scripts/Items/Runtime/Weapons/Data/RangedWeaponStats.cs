using System;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 遠距離武器の計算に要る材料。UnityEngine に依存しない。
    /// </summary>
    public struct RangedWeaponInputs
    {
        /// <summary>武器の強さ（DPS）。</summary>
        public float Strength;

        /// <summary>持ち主の基礎攻撃力。</summary>
        public float CharacterAttack;

        /// <summary>基礎攻撃力を DPS に足すときの係数。</summary>
        public float CharacterAttackWeight;

        /// <summary>撃つ間隔（秒、速射の補正前）。</summary>
        public float FireInterval;

        /// <summary>矢の雨の続く時間（秒、持続時間の補正前）。雨を降らせない武器種では 0。</summary>
        public float RainDuration;

        /// <summary>矢の雨がダメージを与える間隔（秒）。</summary>
        public float RainTickInterval;

        public EnchantmentTotals Enchantments;

        /// <summary>補正前のクリティカル率（0〜1）とクリティカル倍率。</summary>
        public float BaseCritChance;
        public float BaseCritMultiplier;

        /// <summary>永続アップグレードなど、外から掛かるクリティカルの補正。</summary>
        public Func<float, float> CritChanceModifier;
        public Func<float, float> CritMultiplierModifier;
    }

    /// <summary>
    /// 遠距離武器の実際の数値。「強さ」は DPS なので、1 発のダメージは撃つ間隔から逆算する。
    ///
    ///   1 発 = (強さ ＋ 基礎攻撃力 × 係数) × 撃つ間隔 × (1 ＋ ダメージ増加)
    ///
    /// 撃つ間隔は速射の補正前で配るので、速射は間隔だけ縮めて DPS を上げる（近接の 1 周と同じ考え方）。
    /// 数で増える矢・多重の一斉射・爆発は 1 発と同じ（爆発は割合）ダメージを別に当てる。どれもここの DPS には含めない。
    ///
    /// 矢の雨（持続弓）は、範囲にずっと居た 1 体が受ける合計が 1 発になるように、補正前の刻みの数で割って毎刻み与える。
    /// 持続時間のエンチャントは 1 刻みの量はそのままで刻みを増やす（合計が増える）。
    /// </summary>
    public sealed class RangedWeaponStats
    {
        private readonly double rawShot;
        private readonly int baseRainTicks;

        private RangedWeaponStats(double rawShot, int baseRainTicks)
        {
            this.rawShot = rawShot;
            this.baseRainTicks = baseRainTicks;
        }

        /// <summary>攻撃速度の倍率（1 ＋ 速射）。</summary>
        public float AttackSpeed { get; private set; } = 1f;

        /// <summary>撃つ間隔（秒、速射の補正後）。</summary>
        public float FireInterval { get; private set; }

        /// <summary>1 発のダメージ（クリティカル前）。四捨五入、最低 1。</summary>
        public int ShotDamage => Math.Max(1, RoundToInt(rawShot));

        public float CritChance { get; private set; }
        public float CritMultiplier { get; private set; } = 1f;

        /// <summary>ノックバックの上乗せ（m/s）。</summary>
        public float KnockbackBonus { get; private set; }

        /// <summary>スタン・気絶の判定で相対ダメージ量に掛ける倍率。</summary>
        public float ReactionScale { get; private set; } = 1f;

        /// <summary>命中時の爆発の威力（1 発に対する割合）。0 なら爆発しない。</summary>
        public float ExplosionRatio { get; private set; }

        /// <summary>爆発の半径（m、サイズの補正後）。</summary>
        public float ExplosionRadius { get; private set; }

        /// <summary>ドロップ率の上乗せ（0.1 で +10%）。ドロップの仕組みが読む。</summary>
        public float DropRateBonus { get; private set; }

        /// <summary>同時に放つ矢・同時に降らせる雨を本撃の横に足す数（「数」の合計の切り捨て）。</summary>
        public int ExtraProjectiles { get; private set; }

        /// <summary>「多重」の合計の切り捨て。弓は遅れて放つ一斉射の回数、持続弓は同じ所にもう一度降らせる回数。</summary>
        public int MultishotCount { get; private set; }

        /// <summary>矢が貫ける敵の数（「貫通」の合計の切り捨て）。0 なら最初の敵で止まる。</summary>
        public int PierceCount { get; private set; }

        /// <summary>矢が敵を追うか（ホーミングが 1 つでも付いている）。</summary>
        public bool Homing { get; private set; }

        /// <summary>矢の速さの倍率（1 ＋ 弾速）。雨は降り始めるまでの時間がこの分縮む。</summary>
        public float ProjectileSpeedScale { get; private set; } = 1f;

        /// <summary>範囲の大きさの倍率（1 ＋ サイズ）。雨の半径と爆発の半径に掛かる。</summary>
        public float SizeScale { get; private set; } = 1f;

        /// <summary>雨の続く時間の倍率（1 ＋ 持続時間）。</summary>
        public float DurationScale { get; private set; } = 1f;

        /// <summary>雨がダメージを与える回数（持続時間の補正後）。雨を降らせない武器種では 0。</summary>
        public int RainTickCount { get; private set; }

        /// <summary>雨の 1 刻みのダメージ（クリティカル前）。四捨五入、最低 1。雨を降らせない武器種では 0。</summary>
        public int RainTickDamage => baseRainTicks > 0 ? Math.Max(1, RoundToInt(rawShot / baseRainTicks)) : 0;

        /// <summary>クリティカル込みの平均 DPS（1 体に 1 本ずつ当たるとして。数・多重・爆発は含めない）。表示用。</summary>
        public float AverageDps
        {
            get
            {
                if (FireInterval <= 0f) return 0f;
                float critFactor = 1f + CritChance * (CritMultiplier - 1f);
                int perShot = baseRainTicks > 0 ? RainTickDamage * baseRainTicks : ShotDamage;
                return perShot * critFactor / FireInterval;
            }
        }

        /// <summary>クリティカルを掛けた 1 撃。四捨五入、最低 1。</summary>
        public int ApplyCritical(int damage) => Math.Max(1, RoundToInt(damage * CritMultiplier));

        /// <summary>1 撃 hit に添える爆発のダメージ。0 なら爆発しない。</summary>
        public int ExplosionDamage(int hit) =>
            ExplosionRatio > 0f && ExplosionRadius > 0f && hit > 0 ? Math.Max(1, RoundToInt(hit * ExplosionRatio)) : 0;

        public static RangedWeaponStats Compute(RangedWeaponInputs inputs)
        {
            EnchantmentTotals enchant = inputs.Enchantments ?? EnchantmentTotals.Empty;

            float baseInterval = Math.Max(0f, inputs.FireInterval);
            float speed = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.RapidFire));
            float source = Math.Max(0f, inputs.Strength + inputs.CharacterAttack * inputs.CharacterAttackWeight);
            float damageUp = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.DamageUp));
            double shot = (double)source * baseInterval * damageUp;

            float durationScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.Duration));
            int baseTicks = TickCount(inputs.RainDuration, inputs.RainTickInterval);
            int ticks = TickCount(inputs.RainDuration * durationScale, inputs.RainTickInterval);

            float critChance = inputs.BaseCritChance + enchant.Amount(EnchantmentKind.CritChance);
            if (inputs.CritChanceModifier != null) critChance = inputs.CritChanceModifier(critChance);
            float critMultiplier = inputs.BaseCritMultiplier;
            if (inputs.CritMultiplierModifier != null) critMultiplier = inputs.CritMultiplierModifier(critMultiplier);

            float sizeScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.Size));
            return new RangedWeaponStats(shot, baseTicks)
            {
                AttackSpeed = speed,
                FireInterval = baseInterval / speed,
                CritChance = Clamp01(critChance),
                CritMultiplier = Math.Max(1f, critMultiplier),
                KnockbackBonus = Math.Max(0f, enchant.Amount(EnchantmentKind.Knockback)),
                ReactionScale = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.Stun)),
                ExplosionRatio = Math.Max(0f, enchant.Amount(EnchantmentKind.Explosion)),
                ExplosionRadius = Math.Max(0f, enchant.Secondary(EnchantmentKind.Explosion)) * sizeScale,
                DropRateBonus = Math.Max(0f, enchant.Amount(EnchantmentKind.DropUp)),
                ExtraProjectiles = FloorCount(enchant.Amount(EnchantmentKind.ProjectileCount)),
                MultishotCount = FloorCount(enchant.Amount(EnchantmentKind.Multishot)),
                PierceCount = FloorCount(enchant.Amount(EnchantmentKind.Pierce)),
                Homing = enchant.Has(EnchantmentKind.Homing),
                ProjectileSpeedScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.ProjectileSpeed)),
                SizeScale = sizeScale,
                DurationScale = durationScale,
                RainTickCount = ticks,
            };
        }

        /// <summary>duration 秒を interval 秒ごとに刻む回数（四捨五入、最低 1）。どちらかが 0 以下なら 0（雨を降らせない）。</summary>
        private static int TickCount(float duration, float interval)
        {
            if (duration <= 0f || interval <= 0f) return 0;
            return Math.Max(1, RoundToInt(duration / (double)interval));
        }

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
