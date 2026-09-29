using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>近接武器の数値（DPS からの 1 撃の逆算とエンチャント）を確かめる。</summary>
    public sealed class MeleeWeaponStatsTests
    {
        // 片手近距離の仮の段: 比重 1/1/1/2、時間 0.45/0.45/0.5/0.8（1 周 2.2 秒）。
        private static readonly float[] Weights = { 1f, 1f, 1f, 2f };
        private static readonly float[] Durations = { 0.45f, 0.45f, 0.5f, 0.8f };

        private static MeleeWeaponStats Compute(float strength, EnchantmentTotals enchantments = null,
            float characterAttack = 0f, float weight = 1f)
        {
            return MeleeWeaponStats.Compute(new MeleeWeaponInputs
            {
                Strength = strength,
                CharacterAttack = characterAttack,
                CharacterAttackWeight = weight,
                StepWeights = Weights,
                StepDurations = Durations,
                Enchantments = enchantments,
                BaseCritChance = 0.05f,
                BaseCritMultiplier = 1.5f,
            });
        }

        private static int CycleDamage(MeleeWeaponStats stats)
        {
            int sum = 0;
            for (int i = 0; i < stats.StepCount; i++) sum += stats.HitDamage(i);
            return sum;
        }

        private static EnchantmentTotals With(EnchantmentKind kind, float amount, int count = 1, float secondary = 0f)
        {
            var totals = new EnchantmentTotals();
            totals.Add(kind, amount, secondary, count);
            return totals;
        }

        [Test]
        public void 強さはDPSで_1周のダメージは強さに1周の時間を掛けたものになる()
        {
            MeleeWeaponStats stats = Compute(100f);

            Assert.AreEqual(2.2f, stats.CycleDuration, 1e-4f);
            // 100 × 2.2 = 220 を 1:1:1:2 に配る。
            Assert.AreEqual(44, stats.HitDamage(0));
            Assert.AreEqual(44, stats.HitDamage(1));
            Assert.AreEqual(44, stats.HitDamage(2));
            Assert.AreEqual(88, stats.HitDamage(3));
            Assert.AreEqual(220, CycleDamage(stats));
        }

        [Test]
        public void 素手の2段は同じ重さで_エンチャント無しでも1撃が出る()
        {
            // 素手の仮の段: 比重 1/1、時間 0.45/0.45（1 周 0.9 秒）。強さ 3。
            MeleeWeaponStats stats = MeleeWeaponStats.Compute(new MeleeWeaponInputs
            {
                Strength = 3f,
                CharacterAttackWeight = 1f,
                StepWeights = new[] { 1f, 1f },
                StepDurations = new[] { 0.45f, 0.45f },
                Enchantments = EnchantmentTotals.Empty,
                BaseCritChance = 0.05f,
                BaseCritMultiplier = 1.5f,
            });

            Assert.AreEqual(2, stats.StepCount);
            Assert.AreEqual(1, stats.HitDamage(0)); // 3 × 0.9 ÷ 2 = 1.35 → 1
            Assert.AreEqual(stats.HitDamage(0), stats.HitDamage(1));
            Assert.AreEqual(1f, stats.AttackSpeed);
        }

        [Test]
        public void 四段目は他の段より強い()
        {
            MeleeWeaponStats stats = Compute(37f);
            Assert.Greater(stats.HitDamage(3), stats.HitDamage(0));
        }

        [Test]
        public void 一撃は最低1()
        {
            MeleeWeaponStats stats = Compute(0.1f);
            for (int i = 0; i < stats.StepCount; i++) Assert.AreEqual(1, stats.HitDamage(i));
        }

        [Test]
        public void 基礎攻撃力は係数を掛けて強さに足す()
        {
            Assert.AreEqual(CycleDamage(Compute(30f)), CycleDamage(Compute(10f, characterAttack: 20f)));
            Assert.AreEqual(CycleDamage(Compute(20f)), CycleDamage(Compute(10f, characterAttack: 20f, weight: 0.5f)));
        }

        [Test]
        public void ダメージ増加は重ねた数だけ足して掛かる()
        {
            MeleeWeaponStats stats = Compute(100f, With(EnchantmentKind.DamageUp, 0.15f, count: 2));
            Assert.AreEqual(Compute(130f).HitDamage(0), stats.HitDamage(0));
        }

        [Test]
        public void 速射はモーションを縮め_1撃は変えずにDPSを上げる()
        {
            MeleeWeaponStats plain = Compute(100f);
            MeleeWeaponStats rapid = Compute(100f, With(EnchantmentKind.RapidFire, 0.25f));

            Assert.AreEqual(1.25f, rapid.AttackSpeed, 1e-5f);
            Assert.AreEqual(plain.HitDamage(0), rapid.HitDamage(0));
            Assert.AreEqual(plain.StepDuration(3) / 1.25f, rapid.StepDuration(3), 1e-5f);
            Assert.Greater(rapid.AverageDps, plain.AverageDps);
        }

        [Test]
        public void コンボボーナスは段が進むほど上乗せが増える()
        {
            MeleeWeaponStats stats = Compute(100f, With(EnchantmentKind.ComboBonus, 0.1f));

            Assert.AreEqual(44, stats.HitDamage(0), "1 段目は上乗せ無し");
            Assert.AreEqual(48, stats.HitDamage(1)); // 44 × 1.1 = 48.4
            Assert.AreEqual(53, stats.HitDamage(2)); // 44 × 1.2 = 52.8
            Assert.AreEqual(114, stats.HitDamage(3)); // 88 × 1.3 = 114.4
        }

        [Test]
        public void クリティカルは基礎に足し_外からの補正も掛かる()
        {
            MeleeWeaponStats stats = Compute(100f, With(EnchantmentKind.CritChance, 0.05f, count: 3));
            Assert.AreEqual(0.2f, stats.CritChance, 1e-5f);
            Assert.AreEqual(1.5f, stats.CritMultiplier, 1e-5f);
            Assert.AreEqual(66, stats.ApplyCritical(44));

            MeleeWeaponStats meta = MeleeWeaponStats.Compute(new MeleeWeaponInputs
            {
                Strength = 100f,
                StepWeights = Weights,
                StepDurations = Durations,
                BaseCritChance = 0.05f,
                BaseCritMultiplier = 1.5f,
                CritChanceModifier = x => x + 0.1f,
                CritMultiplierModifier = x => x * 2f,
            });
            Assert.AreEqual(0.15f, meta.CritChance, 1e-5f);
            Assert.AreEqual(3f, meta.CritMultiplier, 1e-5f);
        }

        [Test]
        public void クリティカル率は1で頭打ち()
        {
            MeleeWeaponStats stats = Compute(100f, With(EnchantmentKind.CritChance, 0.5f, count: 5));
            Assert.AreEqual(1f, stats.CritChance);
        }

        [Test]
        public void サイズ_ノックバック_スタン_爆発_ドロップを束ねる()
        {
            var totals = new EnchantmentTotals();
            totals.Add(EnchantmentKind.Size, 0.15f, 0f, 2);
            totals.Add(EnchantmentKind.Knockback, 2f);
            totals.Add(EnchantmentKind.Stun, 0.25f);
            totals.Add(EnchantmentKind.Explosion, 0.4f, 2.5f);
            totals.Add(EnchantmentKind.DropUp, 0.1f);

            MeleeWeaponStats stats = Compute(100f, totals);

            Assert.AreEqual(1.3f, stats.HitboxScale, 1e-5f);
            Assert.AreEqual(2f, stats.KnockbackBonus, 1e-5f);
            Assert.AreEqual(1.25f, stats.ReactionScale, 1e-5f);
            Assert.AreEqual(2.5f, stats.ExplosionRadius, 1e-5f);
            Assert.AreEqual(18, stats.ExplosionDamage(44)); // 44 × 0.4 = 17.6
            Assert.AreEqual(0.1f, stats.DropRateBonus, 1e-5f);
        }

        [Test]
        public void 爆発が無ければ爆発のダメージは0()
        {
            Assert.AreEqual(0, Compute(100f).ExplosionDamage(44));
        }

        [Test]
        public void 仕様の武器3本の1撃_キャラ攻撃力なし()
        {
            // 錆びた片手剣 5 / 鉄の片手剣 21 / 蒼鋼の細剣 37 を 1 周 2.2 秒・比重 5 で配る。
            Assert.AreEqual(new[] { 2, 2, 2, 4 }, Hits(Compute(5f)));
            Assert.AreEqual(new[] { 9, 9, 9, 18 }, Hits(Compute(21f)));
            Assert.AreEqual(new[] { 16, 16, 16, 33 }, Hits(Compute(37f)));
        }

        private static int[] Hits(MeleeWeaponStats stats)
        {
            var result = new int[stats.StepCount];
            for (int i = 0; i < result.Length; i++) result[i] = stats.HitDamage(i);
            return result;
        }
    }
}
