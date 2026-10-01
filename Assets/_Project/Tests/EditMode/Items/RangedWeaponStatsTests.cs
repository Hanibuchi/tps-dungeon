using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>遠距離武器（弓・持続弓）の数値（DPS からの 1 発の逆算、雨の刻み、エンチャント）を確かめる。</summary>
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
    }
}
