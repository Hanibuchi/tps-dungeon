using NUnit.Framework;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>レベルごとの MaxHP・基礎攻撃力・必要経験値が式どおりか。</summary>
    public sealed class GrowthCurveTests
    {
        private static readonly GrowthCurve Curve = GrowthCurve.Default;

        [TestCase(1, 100)]
        [TestCase(2, 103)]
        [TestCase(10, 129)]
        [TestCase(50, 288)]
        [TestCase(99, 503)]
        public void MaxHp_FollowsFormula(int level, int expected)
        {
            Assert.AreEqual(expected, Curve.MaxHp(level));
        }

        [TestCase(1, 10.0f)]
        [TestCase(10, 11.6f)]
        [TestCase(50, 19.6f)]
        [TestCase(99, 30.0f)]
        public void BaseAttack_FollowsFormula(int level, float expected)
        {
            Assert.AreEqual(expected, Curve.BaseAttack(level), 0.05f);
        }

        [Test]
        public void MaxValues_AreAboutFiveAndThreeTimesTheStart()
        {
            Assert.AreEqual(5.0, Curve.MaxHp(99) / (double)Curve.MaxHp(1), 0.1, "HP は初期値の約 5 倍");
            Assert.AreEqual(3.0, Curve.BaseAttack(99) / Curve.BaseAttack(1), 0.1, "攻撃力は初期値の約 3 倍");
        }

        [TestCase(1, 10)]
        [TestCase(2, 40)]
        [TestCase(10, 1000)]
        [TestCase(98, 96040)]
        public void ExpToNext_IsPolynomial(int level, int expected)
        {
            Assert.AreEqual(expected, Curve.ExpToNext(level));
        }

        [Test]
        public void ExpToNext_IsZeroAtMaxLevel()
        {
            Assert.AreEqual(0, Curve.ExpToNext(99));
            Assert.AreEqual(0, Curve.ExpToNext(150), "上限を超えたレベルは上限として扱う");
        }

        [Test]
        public void TotalExp_IsSumOfSteps()
        {
            Assert.AreEqual(0, Curve.TotalExpToReach(1));
            Assert.AreEqual(10, Curve.TotalExpToReach(2));
            Assert.AreEqual(2850, Curve.TotalExpToReach(10));
            Assert.AreEqual(3185490, Curve.TotalExpToReach(99));
        }

        [Test]
        public void Level_IsClampedToRange()
        {
            Assert.AreEqual(Curve.MaxHp(1), Curve.MaxHp(0));
            Assert.AreEqual(Curve.MaxHp(99), Curve.MaxHp(200));
        }

        [Test]
        public void Table_ListsEveryTenLevelsAndTheMax()
        {
            string table = GrowthTable.Format(Curve);
            StringAssert.Contains("| 1 | 100 |", table);
            StringAssert.Contains("| 50 | 288 |", table);
            StringAssert.Contains("| 99 | 503 |", table);
        }
    }
}
