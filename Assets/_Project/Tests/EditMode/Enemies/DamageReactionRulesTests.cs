using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>スタン・気絶の確率が相対ダメージ量に比例するか。</summary>
    public sealed class DamageReactionRulesTests
    {
        private static readonly DamageReactionRules Rules = new DamageReactionRules(2f, 0.5f);

        [Test]
        public void ChanceIsProportionalToRelativeDamage()
        {
            Assert.AreEqual(0.2f, Rules.StunChance(10, 100), 1e-5f);
            Assert.AreEqual(0.05f, Rules.FaintChance(10, 100), 1e-5f);
            Assert.AreEqual(0.4f, Rules.StunChance(20, 100), 1e-5f, "倍のダメージで倍の確率");
            Assert.AreEqual(0.1f, Rules.FaintChance(20, 100), 1e-5f);
        }

        [Test]
        public void ChanceCapsAtOne()
        {
            Assert.AreEqual(1f, Rules.StunChance(50, 100), 1e-5f);
            Assert.AreEqual(1f, Rules.StunChance(500, 100), 1e-5f);
            Assert.AreEqual(0.5f, Rules.FaintChance(500, 100), 1e-5f, "相対ダメージ量は 1 で頭打ち");
        }

        [Test]
        public void FaintIsRolledFirst()
        {
            // 気絶 5%・スタン 20%
            Assert.AreEqual(DamageReaction.Faint, Rules.Roll(10, 100, false, 0.04f, 0f));
            Assert.AreEqual(DamageReaction.Stun, Rules.Roll(10, 100, false, 0.05f, 0.19f));
            Assert.AreEqual(DamageReaction.None, Rules.Roll(10, 100, false, 0.05f, 0.2f));
        }

        [Test]
        public void NothingOnZeroOrKillingBlow()
        {
            Assert.AreEqual(DamageReaction.None, Rules.Roll(0, 100, false, 0f, 0f));
            Assert.AreEqual(DamageReaction.None, Rules.Roll(100, 100, true, 0f, 0f));
        }

        [Test]
        public void ZeroFactorNeverTriggers()
        {
            var rules = new DamageReactionRules(0f, 0f);
            Assert.AreEqual(DamageReaction.None, rules.Roll(100, 100, false, 0f, 0f));
        }
    }
}
