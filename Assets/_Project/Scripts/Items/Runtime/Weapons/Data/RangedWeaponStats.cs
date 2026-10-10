using System;
using TpsDungeon.Player;

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

        /// <summary>炎を吐き続けられる時間（秒、持続時間の補正前）。炎を吐かない武器種では 0。</summary>
        public float FlameDuration;

        /// <summary>炎がダメージを与える間隔（秒、速射の補正前）。</summary>
        public float FlameTickInterval;

        /// <summary>治癒の場の続く時間（秒、持続時間の補正前）。治癒の場を張らない武器種では 0。</summary>
        public float HealDuration;

        /// <summary>治癒の場が回復する間隔（秒）。</summary>
        public float HealTickInterval;

        /// <summary>召喚した置物が居る時間（秒、持続時間の補正前）。召喚しない武器種では 0。</summary>
        public float SummonDuration;

        /// <summary>加護（ダメージ軽減）の続く時間（秒、持続時間の補正前）。加護を張らない武器種では 0。</summary>
        public float BlessingDuration;

        /// <summary>張る加護の種類（武器ごと）。</summary>
        public BlessingKind BlessingKind;

        /// <summary>ダメージ軽減・治癒で 1 回に掛ける人数（数の補正前）。</summary>
        public int SupportTargetCount;

        /// <summary>強さ 1 あたりの加護の値（種類ごとの武器種の係数）と、値の上限。</summary>
        public float BlessingPerStrength;
        public float BlessingCap;

        /// <summary>仲間をすぐ回復する武器種（治癒）か。強さを 1 人への回復量と読む。</summary>
        public bool InstantHeal;

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
    ///
    /// 炎（火炎放射器）は、吐き続けられる時間と、吐き終えてからの待ち（撃つ間隔）を合わせた 1 周で 強さ × 1 周 になるよう、
    /// 補正前の刻みの数で割って毎刻み与える（炎の中にずっと居た 1 体が受ける DPS がおよそ強さになる）。
    /// 速射は刻みの間隔と待ちの両方を縮め、持続時間は吐ける時間を延ばして刻みを増やす。
    ///
    /// 治癒の場（治癒持続）は「強さ」を毎秒の回復量と読み、雨と同じく、場にずっと居た 1 人が回復する合計が
    /// 強さ × 撃つ間隔 × (1 ＋ 回復量増加) になるよう、補正前の刻みの数で割って毎刻み回復する。持続時間は刻みを増やす。クリティカルは無い。
    ///
    /// 召喚は攻撃しないので、ダメージの値は使わない。置物が居る時間（持続時間で延びる）と、数・速射だけを使う。
    ///
    /// ダメージ軽減は「強さ」を加護の強さと読み、武器ごとの種類（被ダメージ軽減・クリティカル倍率・状態異常耐性）の係数を武器種から引いて掛ける。
    /// 持続時間は加護の続く時間を延ばす。治癒は「強さ」を 1 人への回復量と読み、回復量増加を掛ける。
    /// どちらも 武器種の人数 ＋ 数 人に掛け、多重で選び直してもう一度掛ける。クリティカルは無い。
    /// </summary>
    public sealed class RangedWeaponStats
    {
        private readonly double rawShot;
        private readonly int baseRainTicks;
        private readonly double rawFlameTick;
        private readonly double rawHealTick;

        private RangedWeaponStats(double rawShot, int baseRainTicks, double rawFlameTick, double rawHealTick)
        {
            this.rawShot = rawShot;
            this.baseRainTicks = baseRainTicks;
            this.rawFlameTick = rawFlameTick;
            this.rawHealTick = rawHealTick;
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

        /// <summary>同時に放つ矢・降らせる雨・連ねる列・吐く炎の筋を本撃の横に足す数（「数」の合計の切り捨て）。雷は飛び移る回数を増やす。</summary>
        public int ExtraProjectiles { get; private set; }

        /// <summary>「多重」の合計の切り捨て。弓は遅れて放つ一斉射の回数、持続弓は同じ所にもう一度降らせる回数、雷と連置は遅れてもう一度放つ回数。</summary>
        public int MultishotCount { get; private set; }

        /// <summary>矢が貫ける敵の数（「貫通」の合計の切り捨て）。0 なら最初の敵で止まる。</summary>
        public int PierceCount { get; private set; }

        /// <summary>矢が敵を追うか（ホーミングが 1 つでも付いている）。</summary>
        public bool Homing { get; private set; }

        /// <summary>矢の速さの倍率（1 ＋ 弾速）。雨は降り始めるまでの時間がこの分縮み、炎は届く距離がこの分伸びる。</summary>
        public float ProjectileSpeedScale { get; private set; } = 1f;

        /// <summary>範囲の大きさの倍率（1 ＋ サイズ）。雨の半径・爆発の半径・炎の太さ・治癒の場の半径に掛かる。</summary>
        public float SizeScale { get; private set; } = 1f;

        /// <summary>続く時間の倍率（1 ＋ 持続時間）。雨の長さ・炎を吐ける時間・連置の列の長さ・治癒の場の長さ・置物の居る時間に掛かる。</summary>
        public float DurationScale { get; private set; } = 1f;

        /// <summary>雨がダメージを与える回数（持続時間の補正後）。雨を降らせない武器種では 0。</summary>
        public int RainTickCount { get; private set; }

        /// <summary>雨の 1 刻みのダメージ（クリティカル前）。四捨五入、最低 1。雨を降らせない武器種では 0。</summary>
        public int RainTickDamage => baseRainTicks > 0 ? Math.Max(1, RoundToInt(rawShot / baseRainTicks)) : 0;

        /// <summary>炎を吐き続けられる時間（秒、持続時間の補正後）。炎を吐かない武器種では 0。</summary>
        public float FlameDuration { get; private set; }

        /// <summary>炎がダメージを与える間隔（秒、速射の補正後）。</summary>
        public float FlameTickInterval { get; private set; }

        /// <summary>吐き続けたときに炎がダメージを与える回数（持続時間・速射の補正後）。炎を吐かない武器種では 0。</summary>
        public int FlameTickCount { get; private set; }

        /// <summary>炎の 1 刻みのダメージ（クリティカル前）。四捨五入、最低 1。炎を吐かない武器種では 0。</summary>
        public int FlameTickDamage => rawFlameTick > 0 ? Math.Max(1, RoundToInt(rawFlameTick)) : 0;

        /// <summary>治癒の場が回復する回数（持続時間の補正後）。治癒の場を張らない武器種では 0。</summary>
        public int HealTickCount { get; private set; }

        /// <summary>治癒の場の 1 刻みの回復量。四捨五入、最低 1。治癒の場を張らない武器種では 0。</summary>
        public int HealTickAmount => rawHealTick > 0 ? Math.Max(1, RoundToInt(rawHealTick)) : 0;

        /// <summary>場にずっと居た 1 人が 1 つの場から回復する合計（1 刻み × 刻みの数）。表示用。</summary>
        public int HealPerField => HealTickAmount * HealTickCount;

        /// <summary>召喚した置物が居る時間（秒、持続時間の補正後）。召喚しない武器種では 0。</summary>
        public float SummonDuration { get; private set; }

        /// <summary>1 回で呼び出す置物の数（本体 ＋ 数）。</summary>
        public int SummonCount => 1 + ExtraProjectiles;

        /// <summary>加護の続く時間（秒、持続時間の補正後）。加護を張らない武器種では 0。</summary>
        public float BlessingDuration { get; private set; }

        /// <summary>張る加護の種類。</summary>
        public BlessingKind BlessingKind { get; private set; }

        /// <summary>
        /// 加護の値（強さ × 係数、上限まで）。被ダメージ軽減は割合（0.2 で 2 割減る）、クリティカル倍率は上げ幅（1 でクリティカルのダメージが 2 倍）、状態異常耐性は割合（1 で無効）。
        /// </summary>
        public float BlessingAmount { get; private set; }

        /// <summary>治癒の 1 人への回復量（回復量増加の補正後）。四捨五入、最低 1。すぐ回復しない武器種では 0。</summary>
        public int HealAmount { get; private set; }

        /// <summary>ダメージ軽減・治癒で 1 回に掛ける人数（武器種の人数 ＋ 数、最低 1）。</summary>
        public int TargetCount => Math.Max(1, SupportTargetCount) + ExtraProjectiles;

        private int SupportTargetCount { get; set; }

        /// <summary>敵を傷つけない武器種（治癒持続・召喚・ダメージ軽減・治癒）か。DPS は 0 と数える。</summary>
        public bool IsSupport => rawHealTick > 0 || SummonDuration > 0f || BlessingDuration > 0f || HealAmount > 0;

        /// <summary>
        /// クリティカル込みの平均 DPS（1 体に 1 本ずつ当たるとして。数・多重・爆発は含めない）。表示用。
        /// 炎は吐き続けて待つ 1 周の平均（持続時間の延びは含めない）。
        /// </summary>
        public float AverageDps
        {
            get
            {
                if (IsSupport) return 0f;
                if (FlameTickCount > 0)
                {
                    float burn = FlameDuration / DurationScale;
                    float cycle = burn + FireInterval;
                    if (cycle <= 0f || FlameTickInterval <= 0f) return 0f;
                    float flameCrit = 1f + CritChance * (CritMultiplier - 1f);
                    return FlameTickDamage * (burn / FlameTickInterval) * flameCrit / cycle;
                }

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

            // 炎は 吐ける時間 ＋ 待ち の 1 周ぶんを、補正前の刻みで割る。
            double flameTick = 0;
            int flameTicks = 0;
            float flameDuration = 0f;
            float flameInterval = 0f;
            int baseFlameTicks = TickCount(inputs.FlameDuration, inputs.FlameTickInterval);
            if (baseFlameTicks > 0)
            {
                flameTick = (double)source * (baseInterval + inputs.FlameDuration) * damageUp / baseFlameTicks;
                flameDuration = inputs.FlameDuration * durationScale;
                flameInterval = inputs.FlameTickInterval / speed;
                flameTicks = TickCount(flameDuration, flameInterval);
            }

            // 治癒の場は 強さ × 撃つ間隔 × (1 ＋ 回復量増加) を補正前の刻みで割る。
            double healTick = 0;
            int baseHealTicks = TickCount(inputs.HealDuration, inputs.HealTickInterval);
            if (baseHealTicks > 0)
            {
                float healUp = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.HealUp));
                healTick = (double)source * baseInterval * healUp / baseHealTicks;
            }

            // 加護は武器の強さだけで決まる（基礎攻撃力は足さない）。
            float blessingDuration = Math.Max(0f, inputs.BlessingDuration) * durationScale;
            float blessingStrength = blessingDuration > 0f ? Math.Max(0f, inputs.Strength) : 0f;
            int healAmount = 0;
            if (inputs.InstantHeal)
            {
                float healUp = Math.Max(0f, 1f + enchant.Amount(EnchantmentKind.HealUp));
                healAmount = Math.Max(1, RoundToInt((double)Math.Max(0f, inputs.Strength) * healUp));
            }

            float sizeScale = Math.Max(0.1f, 1f + enchant.Amount(EnchantmentKind.Size));
            return new RangedWeaponStats(shot, baseTicks, flameTick, healTick)
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
                FlameDuration = flameDuration,
                FlameTickInterval = flameInterval,
                FlameTickCount = flameTicks,
                HealTickCount = baseHealTicks > 0 ? TickCount(inputs.HealDuration * durationScale, inputs.HealTickInterval) : 0,
                SummonDuration = Math.Max(0f, inputs.SummonDuration) * durationScale,
                BlessingDuration = blessingDuration,
                BlessingKind = inputs.BlessingKind,
                SupportTargetCount = inputs.SupportTargetCount,
                BlessingAmount = Clamp(blessingStrength * inputs.BlessingPerStrength, 0f, Math.Max(0f, inputs.BlessingCap)),
                HealAmount = healAmount,
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

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
