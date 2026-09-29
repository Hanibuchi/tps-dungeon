using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>スタンはしきい値で必ず、気絶は相対ダメージ量に比例する確率で起きるか。</summary>
    public sealed class DamageReactionRulesTests
    {
        private static readonly DamageReactionRules Rules = new DamageReactionRules(0.1f, 0.5f);

        [Test]
        public void StunAlwaysAtOrAboveThreshold()
        {
            Assert.IsFalse(Rules.Stuns(9, 100));
            Assert.IsTrue(Rules.Stuns(10, 100), "ちょうどしきい値で起きる");
            Assert.IsTrue(Rules.Stuns(80, 100));
        }

        [Test]
        public void FaintChanceIsProportionalToRelativeDamage()
        {
            Assert.AreEqual(0.05f, Rules.FaintChance(10, 100), 1e-5f);
            Assert.AreEqual(0.1f, Rules.FaintChance(20, 100), 1e-5f, "倍のダメージで倍の確率");
            Assert.AreEqual(0.5f, Rules.FaintChance(500, 100), 1e-5f, "相対ダメージ量は 1 で頭打ち");
        }

        [Test]
        public void FaintIsRolledFirst_ThenStunIsCertain()
        {
            // 気絶 5%
            Assert.AreEqual(DamageReaction.Faint, Rules.Roll(10, 100, false, 0.04f));
            Assert.AreEqual(DamageReaction.Stun, Rules.Roll(10, 100, false, 0.99f));
        }

        [Test]
        public void BelowThreshold_OnlyFaintCanHappen()
        {
            // 最大 HP の 5%: 気絶 2.5%、スタンはしない
            Assert.AreEqual(DamageReaction.Faint, Rules.Roll(5, 100, false, 0.02f));
            Assert.AreEqual(DamageReaction.None, Rules.Roll(5, 100, false, 0.03f));
        }

        [Test]
        public void NothingOnZeroOrKillingBlow()
        {
            Assert.AreEqual(DamageReaction.None, Rules.Roll(0, 100, false, 0f));
            Assert.AreEqual(DamageReaction.None, Rules.Roll(100, 100, true, 0f));
        }

        [Test]
        public void ThresholdAboveOneNeverStuns()
        {
            var rules = new DamageReactionRules(1.1f, 0f);
            Assert.AreEqual(DamageReaction.None, rules.Roll(100, 100, false, 0f));
        }

        [Test]
        public void ScaleMultipliesRelativeDamage_ForStunEnchantment()
        {
            // 最大 HP の 9% はしきい値 10% に届かないが、1.25 倍すると 11.25% で届く。
            Assert.IsFalse(Rules.Stuns(9, 100));
            Assert.IsTrue(Rules.Stuns(9, 100, 1.25f));
            Assert.AreEqual(0.05625f, Rules.FaintChance(9, 100, 1.25f), 1e-5f);
            Assert.AreEqual(DamageReaction.Stun, Rules.Roll(9, 100, false, 0.99f, 1.25f));
            Assert.AreEqual(0.5f, Rules.FaintChance(90, 100, 2f), 1e-5f, "倍率を掛けても 1 で頭打ち");
        }
    }
}
