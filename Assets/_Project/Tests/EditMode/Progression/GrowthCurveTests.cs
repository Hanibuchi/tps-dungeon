using NUnit.Framework;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>レベルごとの MaxHP・基礎攻撃力・必要経験値が式どおりか。</summary>
    public sealed class GrowthCurveTests
    {
        private static readonly GrowthCurve Curve = GrowthCurve.Default;

        [TestCase(1, 100)]
        [TestCase(2, 110)]
        [TestCase(10, 190)]
        [TestCase(50, 590)]
        [TestCase(99, 1080)]
        public void MaxHp_FollowsFormula(int level, int expected)
        {
            Assert.AreEqual(expected, Curve.MaxHp(level));
        }

        [TestCase(1, 20f)]
        [TestCase(10, 38f)]
        [TestCase(50, 118f)]
        [TestCase(100, 218f)]
        public void BaseAttack_FollowsFormula(int level, float expected)
        {
            Assert.AreEqual(expected, Curve.BaseAttack(level), 0.05f);
        }

        [Test]
        public void MaxHp_MatchesBalanceSheet()
        {
            // スプレッドシート「バランス」のプレイヤーシート B 列: 100 ＋ 10 × (L−1)
            for (int level = 1; level <= LevelBalance.MaxLevel; level++)
                Assert.AreEqual(100 + 10 * (level - 1), Curve.MaxHp(level), $"Lv{level}");
        }

        [Test]
        public void BaseAttack_MatchesBalanceSheet()
        {
            // スプレッドシート「バランス」の攻撃力シート B 列: 20 ＋ 2 × (L−1)
            for (int level = 1; level <= LevelBalance.MaxLevel; level++)
                Assert.AreEqual(20f + 2f * (level - 1), Curve.BaseAttack(level), 0.001f, $"Lv{level}");
        }

        [TestCase(1, 100)]
        [TestCase(2, 120)]
        [TestCase(10, 650)]
        [TestCase(50, 2700000)]
        public void ExpToNext_FollowsLevelBalance(int level, int expected)
        {
            Assert.AreEqual(expected, Curve.ExpToNext(level));
            Assert.AreEqual(LevelBalance.RequiredExp(level), Curve.ExpToNext(level));
        }

        [Test]
        public void ExpToNext_IsZeroAtMaxLevel()
        {
            Assert.AreEqual(LevelBalance.MaxLevel, Curve.MaxLevel);
            Assert.AreEqual(0, Curve.ExpToNext(LevelBalance.MaxLevel));
            Assert.AreEqual(0, Curve.ExpToNext(150), "上限を超えたレベルは上限として扱う");
        }

        [Test]
        public void TotalExp_IsSumOfSteps()
        {
            Assert.AreEqual(0, Curve.TotalExpToReach(1));
            Assert.AreEqual(100, Curve.TotalExpToReach(2));
            Assert.AreEqual(2380, Curve.TotalExpToReach(10));
            Assert.AreEqual(3030, Curve.TotalExpToReach(11));
        }

        [Test]
        public void Level_IsClampedToRange()
        {
            Assert.AreEqual(Curve.MaxHp(1), Curve.MaxHp(0));
            Assert.AreEqual(Curve.MaxHp(LevelBalance.MaxLevel), Curve.MaxHp(200));
        }

        [Test]
        public void Table_ListsEveryTenLevelsAndTheMax()
        {
            string table = GrowthTable.Format(Curve);
            StringAssert.Contains("| 1 | 100 |", table);
            StringAssert.Contains("| 50 | 590 |", table);
            StringAssert.Contains($"| {LevelBalance.MaxLevel} | {Curve.MaxHp(LevelBalance.MaxLevel)} |", table);
        }
    }
}
