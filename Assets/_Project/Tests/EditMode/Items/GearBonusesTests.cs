using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>ホットバーのお守り・盾から出る効果の合計。</summary>
    public sealed class GearBonusesTests
    {
        private static EnchantmentTotals Totals(params (EnchantmentKind kind, float amount)[] entries)
        {
            var totals = new EnchantmentTotals();
            foreach (var (kind, amount) in entries) totals.Add(kind, amount);
            return totals;
        }

        private static GearSlot Charm(params (EnchantmentKind kind, float amount)[] entries) =>
            new GearSlot(PassiveGear.Charm, 4f, Totals(entries));

        private static GearSlot Shield(float defense, params (EnchantmentKind kind, float amount)[] entries) =>
            new GearSlot(PassiveGear.Shield, defense, Totals(entries));

        [Test]
        public void お守りはホットバーにある分を全部足す()
        {
            var bonuses = GearBonuses.Compute(new[]
            {
                Charm((EnchantmentKind.MoveSpeed, 0.1f)),
                default,
                Charm((EnchantmentKind.MoveSpeed, 0.1f), (EnchantmentKind.MaxHp, 0.1f)),
            }, false);

            Assert.AreEqual(0.2f, bonuses.MoveSpeedPercent, 1e-5f);
            Assert.AreEqual(0.1f, bonuses.MaxHpPercent, 1e-5f);
            Assert.AreEqual(-1, bonuses.ActiveShieldIndex);
            Assert.AreEqual(1f, bonuses.DamageTaken, 1e-5f);
        }

        [Test]
        public void 盾は片手武器を持っていなければ効かない()
        {
            var bonuses = GearBonuses.Compute(new[] { Shield(30f, (EnchantmentKind.Regen, 1f)) }, false);

            Assert.AreEqual(-1, bonuses.ActiveShieldIndex);
            Assert.AreEqual(0f, bonuses.RegenPerSecond, 1e-5f);
            Assert.AreEqual(1f, bonuses.DamageTaken, 1e-5f);
        }

        [Test]
        public void 盾は防御のいちばん高い1枚だけ効き_そのエンチャントもその分だけ()
        {
            var bonuses = GearBonuses.Compute(new[]
            {
                Shield(6f, (EnchantmentKind.Regen, 5f)),
                Shield(30f, (EnchantmentKind.Regen, 1f)),
                Shield(18f, (EnchantmentKind.Regen, 3f)),
            }, true);

            Assert.AreEqual(1, bonuses.ActiveShieldIndex);
            Assert.AreEqual(30f, bonuses.ShieldDefense, 1e-5f);
            Assert.AreEqual(1f, bonuses.RegenPerSecond, 1e-5f);
        }

        [Test]
        public void 同じ防御の盾なら先の枠()
        {
            var bonuses = GearBonuses.Compute(new[] { default, Shield(18f), Shield(18f) }, true);
            Assert.AreEqual(1, bonuses.ActiveShieldIndex);
        }

        [Test]
        public void 受けるダメージは防御と防御力のエンチャントで減る()
        {
            Assert.AreEqual(100f / 130f, GearBonuses.DamageTakenFor(30f, 0f), 1e-5f, "防御 30 で 100 / 130");
            Assert.AreEqual(100f / 106f * 0.9f, GearBonuses.DamageTakenFor(6f, 0.1f), 1e-5f);
            Assert.AreEqual(0.2f, GearBonuses.DamageTakenFor(0f, 5f), 1e-5f, "エンチャントで減らせるのは 80% まで");
            Assert.AreEqual(1f, GearBonuses.DamageTakenFor(0f, 0f), 1e-5f);
        }

        [Test]
        public void 手の武器に足すのはクリティカル率とドロップ増加と数と多重だけ()
        {
            var bonuses = GearBonuses.Compute(new[]
            {
                Charm((EnchantmentKind.CritChance, 0.05f), (EnchantmentKind.ProjectileCount, 1f), (EnchantmentKind.MoveSpeed, 0.1f)),
                Charm((EnchantmentKind.CritChance, 0.05f), (EnchantmentKind.Multishot, 1f), (EnchantmentKind.DropUp, 0.1f)),
            }, false);

            EnchantmentTotals weapon = bonuses.WeaponTotals;
            Assert.AreEqual(0.1f, weapon.Amount(EnchantmentKind.CritChance), 1e-5f);
            Assert.AreEqual(2, weapon.Stacks(EnchantmentKind.CritChance));
            Assert.AreEqual(1f, weapon.Amount(EnchantmentKind.ProjectileCount), 1e-5f);
            Assert.AreEqual(1f, weapon.Amount(EnchantmentKind.Multishot), 1e-5f);
            Assert.AreEqual(0.1f, weapon.Amount(EnchantmentKind.DropUp), 1e-5f);
            Assert.IsFalse(weapon.Has(EnchantmentKind.MoveSpeed));
        }

        [Test]
        public void AddAllは量と個数を足し_副次は大きい方()
        {
            var a = new EnchantmentTotals();
            a.Add(EnchantmentKind.Explosion, 0.4f, 2f);
            var b = new EnchantmentTotals();
            b.Add(EnchantmentKind.Explosion, 0.4f, 3f);
            b.Add(EnchantmentKind.CritChance, 0.05f);

            a.AddAll(b);

            Assert.AreEqual(0.8f, a.Amount(EnchantmentKind.Explosion), 1e-5f);
            Assert.AreEqual(3f, a.Secondary(EnchantmentKind.Explosion), 1e-5f);
            Assert.AreEqual(2, a.Stacks(EnchantmentKind.Explosion));
            Assert.AreEqual(0.05f, a.Amount(EnchantmentKind.CritChance), 1e-5f);
        }
    }
}
