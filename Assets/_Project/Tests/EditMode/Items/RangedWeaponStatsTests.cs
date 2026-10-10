using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>遠距離武器（弓・持続弓・炎の杖・治癒持続・召喚・ダメージ軽減・治癒）の数値（DPS からの 1 発の逆算、雨と炎と治癒の刻み、置物の居る時間、加護、エンチャント）を確かめる。</summary>
    public sealed class RangedWeaponStatsTests
    {
        private static RangedWeaponStats Bow(float strength, EnchantmentTotals enchantments = null, float characterAttack = 0f)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttack = characterAttack,
                CharacterAttackWeight = 1f,
                FireInterval = 0.8f,
                Enchantments = enchantments,
                BaseCritChance = 0.05f,
                BaseCritMultiplier = 1.5f,
            });
        }

        // 持続弓の仮の値: 撃つ間隔 1.2 秒、雨は 3 秒を 0.5 秒ごと（6 刻み）。
        private static RangedWeaponStats Rain(float strength, EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttackWeight = 1f,
                FireInterval = 1.2f,
                RainDuration = 3f,
                RainTickInterval = 0.5f,
                Enchantments = enchantments,
                BaseCritChance = 0.05f,
                BaseCritMultiplier = 1.5f,
            });
        }

        // 火炎放射器の仮の値: 吐ける時間 2.4 秒を 0.2 秒ごと（12 刻み）、止まってから 1.0 秒待つ。
        private static RangedWeaponStats Flame(float strength, EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttackWeight = 1f,
                FireInterval = 1.0f,
                FlameDuration = 2.4f,
                FlameTickInterval = 0.2f,
                Enchantments = enchantments,
                BaseCritChance = 0f,
                BaseCritMultiplier = 1.5f,
            });
        }

        // 治癒持続の仮の値: 撃つ間隔 4 秒、場は 5 秒を 0.5 秒ごと（10 刻み）。基礎攻撃力は足さない。
        private static RangedWeaponStats Heal(float strength, EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttack = 50f,
                CharacterAttackWeight = 0f,
                FireInterval = 4f,
                HealDuration = 5f,
                HealTickInterval = 0.5f,
                Enchantments = enchantments,
            });
        }

        // 召喚の仮の値: 呼び直しの待ち 8 秒、置物は 12 秒居る。
        private static RangedWeaponStats Summon(EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = 40f,
                FireInterval = 8f,
                SummonDuration = 12f,
                Enchantments = enchantments,
            });
        }

        // ダメージ軽減の仮の値: 撃つ間隔 6 秒、加護は 12 秒。強さ 1 で被ダメージ −1%・クリティカル率 +0.5%・状態異常耐性 2%、軽減は 8 割まで。
        private static RangedWeaponStats Blessing(float strength, EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttack = 50f,
                FireInterval = 6f,
                BlessingDuration = 12f,
                BlessingDamageReductionPerStrength = 0.01f,
                BlessingCritChancePerStrength = 0.005f,
                BlessingResistancePerStrength = 0.02f,
                BlessingMaxDamageReduction = 0.8f,
                Enchantments = enchantments,
            });
        }

        // 治癒の仮の値: 撃つ間隔 3 秒。強さは 1 人への回復量。
        private static RangedWeaponStats InstantHeal(float strength, EnchantmentTotals enchantments = null)
        {
            return RangedWeaponStats.Compute(new RangedWeaponInputs
            {
                Strength = strength,
                CharacterAttack = 50f,
                FireInterval = 3f,
                InstantHeal = true,
                Enchantments = enchantments,
            });
        }

        private static EnchantmentTotals With(EnchantmentKind kind, float amount, int count = 1, float secondary = 0f)
        {
            var totals = new EnchantmentTotals();
            totals.Add(kind, amount, secondary, count);
            return totals;
        }

        [Test]
        public void 強さはDPSで_1発は強さに撃つ間隔を掛けたものになる()
        {
            RangedWeaponStats stats = Bow(100f);

            Assert.AreEqual(0.8f, stats.FireInterval, 1e-5f);
            Assert.AreEqual(80, stats.ShotDamage);
        }

        [Test]
        public void 基礎攻撃力は係数を掛けて強さに足す()
        {
            Assert.AreEqual(Bow(110f).ShotDamage, Bow(100f, characterAttack: 10f).ShotDamage);
        }

        [Test]
        public void 弱い弓でも1発は最低1()
        {
            Assert.AreEqual(1, Bow(0.1f).ShotDamage);
        }

        [Test]
        public void ダメージ増加は1発に掛かる()
        {
            Assert.AreEqual(92, Bow(100f, With(EnchantmentKind.DamageUp, 0.15f)).ShotDamage);
        }

        [Test]
        public void 速射は間隔だけ縮めて1発は変えない()
        {
            RangedWeaponStats stats = Bow(100f, With(EnchantmentKind.RapidFire, 0.1f, 2));

            Assert.AreEqual(1.2f, stats.AttackSpeed, 1e-5f);
            Assert.AreEqual(0.8f / 1.2f, stats.FireInterval, 1e-5f);
            Assert.AreEqual(80, stats.ShotDamage);
        }

        [Test]
        public void 数と多重と貫通は個数の合計を切り捨てて数える()
        {
            var totals = new EnchantmentTotals();
            totals.Add(EnchantmentKind.ProjectileCount, 1f, 0f, 2);
            totals.Add(EnchantmentKind.Multishot, 1f, 0f, 3);
            totals.Add(EnchantmentKind.Pierce, 1f);
            RangedWeaponStats stats = Bow(100f, totals);

            Assert.AreEqual(2, stats.ExtraProjectiles);
            Assert.AreEqual(3, stats.MultishotCount);
            Assert.AreEqual(1, stats.PierceCount);
            Assert.IsFalse(stats.Homing);
        }

        [Test]
        public void ホーミングは1つでも付けば効く()
        {
            Assert.IsTrue(Bow(100f, With(EnchantmentKind.Homing, 1f)).Homing);
        }

        [Test]
        public void 弾速とサイズと持続時間は倍率になる()
        {
            var totals = new EnchantmentTotals();
            totals.Add(EnchantmentKind.ProjectileSpeed, 0.2f);
            totals.Add(EnchantmentKind.Size, 0.15f, 0f, 2);
            totals.Add(EnchantmentKind.Duration, 0.2f);
            RangedWeaponStats stats = Rain(100f, totals);

            Assert.AreEqual(1.2f, stats.ProjectileSpeedScale, 1e-5f);
            Assert.AreEqual(1.3f, stats.SizeScale, 1e-5f);
            Assert.AreEqual(1.2f, stats.DurationScale, 1e-5f);
        }

        [Test]
        public void 爆発は1発に対する割合でサイズで半径が伸びる()
        {
            var totals = new EnchantmentTotals();
            totals.Add(EnchantmentKind.Explosion, 0.4f, 2.5f);
            totals.Add(EnchantmentKind.Size, 0.2f);
            RangedWeaponStats stats = Bow(100f, totals);

            Assert.AreEqual(32, stats.ExplosionDamage(stats.ShotDamage));
            Assert.AreEqual(3f, stats.ExplosionRadius, 1e-5f);
        }

        [Test]
        public void 弓は雨を降らせない()
        {
            RangedWeaponStats stats = Bow(100f);

            Assert.AreEqual(0, stats.RainTickCount);
            Assert.AreEqual(0, stats.RainTickDamage);
        }

        [Test]
        public void 雨にずっと居た1体が受ける合計が1発になる()
        {
            RangedWeaponStats stats = Rain(100f);

            // 1 発 = 100 × 1.2 = 120 を 6 刻みに割る。
            Assert.AreEqual(120, stats.ShotDamage);
            Assert.AreEqual(6, stats.RainTickCount);
            Assert.AreEqual(20, stats.RainTickDamage);
            Assert.AreEqual(100f, stats.AverageDps / (1f + stats.CritChance * (stats.CritMultiplier - 1f)), 1e-3f);
        }

        [Test]
        public void 持続時間は1刻みの量を変えずに刻みを増やす()
        {
            RangedWeaponStats stats = Rain(100f, With(EnchantmentKind.Duration, 0.2f, 2));

            // 3 秒 × 1.4 = 4.2 秒 → 0.5 秒ごとに 8 刻み（四捨五入）。
            Assert.AreEqual(8, stats.RainTickCount);
            Assert.AreEqual(20, stats.RainTickDamage);
        }

        [Test]
        public void 雨の刻みも最低1()
        {
            Assert.AreEqual(1, Rain(0.5f).RainTickDamage);
        }

        [Test]
        public void 炎は吐ける時間と待ちの1周を補正前の刻みで割って毎刻み与える()
        {
            RangedWeaponStats stats = Flame(100f);

            // 100 × (2.4 + 1.0) / 12 = 28.3 → 28
            Assert.AreEqual(12, stats.FlameTickCount);
            Assert.AreEqual(28, stats.FlameTickDamage);
            Assert.AreEqual(2.4f, stats.FlameDuration, 1e-5f);
            Assert.AreEqual(0.2f, stats.FlameTickInterval, 1e-5f);
            Assert.AreEqual(1.0f, stats.FireInterval, 1e-5f);
        }

        [Test]
        public void 炎の平均DPSは1周の平均でおよそ強さになる()
        {
            Assert.AreEqual(100f, Flame(100f).AverageDps, 5f);
        }

        [Test]
        public void 持続時間は炎の1刻みを変えずに刻みを増やす()
        {
            RangedWeaponStats stats = Flame(100f, With(EnchantmentKind.Duration, 0.2f));

            Assert.AreEqual(2.88f, stats.FlameDuration, 1e-5f);
            Assert.AreEqual(14, stats.FlameTickCount);
            Assert.AreEqual(Flame(100f).FlameTickDamage, stats.FlameTickDamage);
        }

        [Test]
        public void 速射は炎の刻みと待ちを縮めて1刻みは変えない()
        {
            RangedWeaponStats stats = Flame(100f, With(EnchantmentKind.RapidFire, 0.25f));

            Assert.AreEqual(0.16f, stats.FlameTickInterval, 1e-5f);
            Assert.AreEqual(0.8f, stats.FireInterval, 1e-5f);
            Assert.AreEqual(15, stats.FlameTickCount);
            Assert.AreEqual(Flame(100f).FlameTickDamage, stats.FlameTickDamage);
        }

        [Test]
        public void 炎を吐かない武器種では炎の刻みは0()
        {
            RangedWeaponStats stats = Bow(100f);

            Assert.AreEqual(0, stats.FlameTickCount);
            Assert.AreEqual(0, stats.FlameTickDamage);
            Assert.AreEqual(0f, stats.FlameDuration);
        }

        [Test]
        public void 治癒の場はずっと居れば強さに撃つ間隔を掛けた分を刻みで回復する()
        {
            RangedWeaponStats stats = Heal(30f);

            Assert.AreEqual(10, stats.HealTickCount);
            Assert.AreEqual(12, stats.HealTickAmount);
            Assert.AreEqual(120, stats.HealPerField);
            Assert.AreEqual(0f, stats.AverageDps, "敵を傷つけないので DPS は 0");
        }

        [Test]
        public void 回復量増加は1刻みを増やし_ダメージ増加は効かない()
        {
            Assert.AreEqual(15, Heal(30f, With(EnchantmentKind.HealUp, 0.25f)).HealTickAmount);
            Assert.AreEqual(12, Heal(30f, With(EnchantmentKind.DamageUp, 0.5f)).HealTickAmount);
        }

        [Test]
        public void 持続時間は治癒の場の刻みを増やして1刻みは変えない()
        {
            RangedWeaponStats stats = Heal(30f, With(EnchantmentKind.Duration, 0.2f));

            Assert.AreEqual(12, stats.HealTickCount);
            Assert.AreEqual(12, stats.HealTickAmount);
        }

        [Test]
        public void 速射は撃つ間隔だけ縮めて治癒の1刻みは変えない()
        {
            RangedWeaponStats stats = Heal(30f, With(EnchantmentKind.RapidFire, 0.25f));

            Assert.AreEqual(3.2f, stats.FireInterval, 1e-5f);
            Assert.AreEqual(12, stats.HealTickAmount);
        }

        [Test]
        public void 治癒の場を張らない武器種では治癒の刻みは0()
        {
            Assert.AreEqual(0, Bow(100f).HealTickCount);
            Assert.AreEqual(0, Bow(100f).HealTickAmount);
        }

        [Test]
        public void 召喚は持続時間で長く居て_数で体数が増える()
        {
            Assert.AreEqual(12f, Summon().SummonDuration, 1e-5f);
            Assert.AreEqual(1, Summon().SummonCount);
            Assert.AreEqual(15f, Summon(With(EnchantmentKind.Duration, 0.25f)).SummonDuration, 1e-5f);
            Assert.AreEqual(3, Summon(With(EnchantmentKind.ProjectileCount, 1f, 2)).SummonCount);
            Assert.AreEqual(0f, Summon().AverageDps);
        }

        [Test]
        public void 召喚しない武器種では居る時間は0()
        {
            Assert.AreEqual(0f, Bow(100f).SummonDuration);
            Assert.IsFalse(Bow(100f).IsSupport);
        }
    
        [Test]
        public void 加護は強さに係数を掛けた効き目で_持続時間で長く続く()
        {
            RangedWeaponStats stats = Blessing(20f);
            Assert.AreEqual(0.2f, stats.BlessingDamageReduction, 1e-5f);
            Assert.AreEqual(0.1f, stats.BlessingCritChance, 1e-5f);
            Assert.AreEqual(0.4f, stats.BlessingResistance, 1e-5f);
            Assert.AreEqual(12f, stats.BlessingDuration, 1e-5f);
            Assert.AreEqual(15f, Blessing(20f, With(EnchantmentKind.Duration, 0.25f)).BlessingDuration, 1e-5f);
            Assert.IsTrue(stats.IsSupport);
            Assert.AreEqual(0f, stats.AverageDps);
        }

        [Test]
        public void 加護の被ダメージ軽減は上限で止まり_耐性は1で止まる()
        {
            RangedWeaponStats stats = Blessing(100f);
            Assert.AreEqual(0.8f, stats.BlessingDamageReduction, 1e-5f);
            Assert.AreEqual(1f, stats.BlessingResistance, 1e-5f);
            Assert.AreEqual(0.5f, stats.BlessingCritChance, 1e-5f);
        }

        [Test]
        public void ダメージ軽減と治癒は1と数の人数に掛け_多重と速射が効く()
        {
            Assert.AreEqual(1, Blessing(20f).TargetCount);
            Assert.AreEqual(3, Blessing(20f, With(EnchantmentKind.ProjectileCount, 1f, 2)).TargetCount);
            Assert.AreEqual(2, InstantHeal(25f, With(EnchantmentKind.Multishot, 1f, 2)).MultishotCount);
            Assert.AreEqual(3f / 1.5f, InstantHeal(25f, With(EnchantmentKind.RapidFire, 0.5f)).FireInterval, 1e-5f);
        }

        [Test]
        public void 治癒は強さを1人への回復量と読み_回復量増加を掛ける()
        {
            Assert.AreEqual(25, InstantHeal(25f).HealAmount);
            Assert.AreEqual(30, InstantHeal(25f, With(EnchantmentKind.HealUp, 0.2f)).HealAmount);
            Assert.AreEqual(25, InstantHeal(25f, With(EnchantmentKind.DamageUp, 0.5f)).HealAmount, "ダメージ増加は効かない");
            Assert.IsTrue(InstantHeal(25f).IsSupport);
            Assert.AreEqual(0f, InstantHeal(25f).AverageDps);
        }

        [Test]
        public void 加護を張らない武器種では加護も治癒も0()
        {
            Assert.AreEqual(0f, Bow(100f).BlessingDuration);
            Assert.AreEqual(0f, Bow(100f).BlessingDamageReduction);
            Assert.AreEqual(0, Bow(100f).HealAmount);
            Assert.AreEqual(0, Heal(30f).HealAmount, "治癒持続は刻みで回復するので、すぐの回復量は 0");
        }
    }
}
