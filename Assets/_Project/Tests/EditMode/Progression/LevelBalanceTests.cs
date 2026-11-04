using System;
using NUnit.Framework;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>レベル・ソウル・経験値の倍率の式（LevelBalance）が、スプレッドシート「バランス」のレベルシートの値と合うか。</summary>
    public sealed class LevelBalanceTests
    {
        [TestCase(101L, 100L)]
        [TestCase(108L, 110L)]
        [TestCase(225L, 230L)]
        [TestCase(27_100L, 27_000L)]
        [TestCase(125_100L, 130_000L)]
        [TestCase(150L, 150L)]
        [TestCase(155L, 160L, Description = "ちょうど .5 は切り上げ")]
        [TestCase(1_250L, 1_300L, Description = "ちょうど .5 は切り上げ")]
        [TestCase(999L, 1_000L, Description = "繰り上がって桁が増える")]
        public void RoundToSignificant_KeepsTwoDigits(long value, long expected)
        {
            Assert.AreEqual(expected, LevelBalance.RoundToSignificant(value, 2));
        }

        [Test]
        public void RoundToSignificant_KeepsTwoDigitValues()
        {
            for (long value = 10; value <= 99; value++) Assert.AreEqual(value, LevelBalance.RoundToSignificant(value, 2));
        }

        [TestCase(1, 100.0, 100, 100L)]
        [TestCase(2, 123.114, 120, 220L)]
        [TestCase(5, 229.740, 230, 790L)]
        [TestCase(10, 649.802, 650, 3_030L)]
        [TestCase(11, 800.000, 800, 3_830L, Description = "1 層（10 レベル）で 8 倍")]
        [TestCase(20, 5_198.415, 5_200, 27_210L)]
        [TestCase(30, 41_587.323, 42_000, 221_210L)]
        [TestCase(50, 2_661_588.652, 2_700_000, 14_299_210L)]
        public void RequiredExp_MatchesTheTable(int level, double baseExp, int need, long cumulative)
        {
            Assert.AreEqual(baseExp, LevelBalance.BaseExp(level), 0.001);
            Assert.AreEqual(need, LevelBalance.RequiredExp(level));
            Assert.AreEqual(cumulative, LevelBalance.CumulativeExp(level));
        }

        [TestCase(123.1, 120L)]
        [TestCase(229.7, 230L)]
        [TestCase(1_450.0, 1_500L, Description = "ちょうど .5 は切り上げ")]
        [TestCase(1_449.99, 1_400L)]
        [TestCase(25.0, 25L, Description = "2 桁以下はそのまま")]
        public void RoundToSignificant_RoundsRealValues(double value, long expected)
        {
            Assert.AreEqual(expected, LevelBalance.RoundToSignificant(value, 2));
        }

        [Test]
        public void RequiredExp_IsCappedToIntButCumulativeIsNot()
        {
            Assert.AreEqual(int.MaxValue, LevelBalance.RequiredExp(99), "Lv99 の need は 710 億で int に収まらない");
            Assert.AreEqual(71_000_000_000L, LevelBalance.RequiredExpLong(99));
            Assert.AreEqual(378_184_499_210L, LevelBalance.CumulativeExp(99), "表の Lv99 の累計（int で頭打ちにせずに足す）");
        }

        [Test]
        public void RequiredExp_IsZeroAtMaxLevel()
        {
            Assert.AreEqual(100, LevelBalance.MaxLevel);
            Assert.AreEqual(0, LevelBalance.RequiredExp(LevelBalance.MaxLevel));
            Assert.Greater(LevelBalance.RequiredExp(LevelBalance.MaxLevel - 1), 0);
        }

        [Test]
        public void TotalExpToReach_IsTheCumulativeOfTheLevelBefore()
        {
            Assert.AreEqual(0, LevelBalance.TotalExpToReach(1));
            Assert.AreEqual(100, LevelBalance.TotalExpToReach(2));
            Assert.AreEqual(LevelBalance.CumulativeExp(9), LevelBalance.TotalExpToReach(10));
        }

        [TestCase(1, 10L)]
        [TestCase(2, 22L)]
        [TestCase(5, 79L)]
        [TestCase(10, 300L)]
        [TestCase(14, 750L)]
        [TestCase(20, 2_700L)]
        [TestCase(30, 22_000L)]
        [TestCase(44, 410_000L)]
        [TestCase(50, 1_400_000L)]
        [TestCase(99, 38_000_000_000L)]
        public void SoulReward_MatchesTheTable(int level, long expected)
        {
            Assert.AreEqual(expected, LevelBalance.SoulReward(level));
        }

        [Test]
        public void SoulReward_DoesNotOverflowIn64Bit()
        {
            long total = 0;
            for (int level = 1; level <= LevelBalance.MaxLevel; level++) total += LevelBalance.SoulReward(level);
            Assert.Greater(total, 20_000_000_000L, "Lv1〜100 の合計は 200 億を超える（32bit では収まらない）");
        }

        [TestCase(0, 1f)]
        [TestCase(1, 2f)]
        [TestCase(2, 4f)]
        [TestCase(3, 8f)]
        [TestCase(4, 16f)]
        public void ExpMultiplier_DoublesPerUnlock(int unlocks, float expected)
        {
            Assert.AreEqual(expected, LevelBalance.ExpMultiplier(unlocks), 1e-6f);
        }

        /// <summary>
        /// 想定している 1 階あたりの基本経験値（n 層目の終わりに Lv10n。1 層 5 階、n 層目の解放数は n−1。経験値ボーナスは入れない）:
        /// 基本(n) = (累計(10n) / 倍率(n) − 累計(10(n−1)) / 倍率(n−1)) / 5
        /// </summary>
        [TestCase(1, 606L)]
        [TestCase(2, 2_115L)]
        [TestCase(3, 8_340L)]
        [TestCase(4, 33_170L)]
        [TestCase(5, 134_510L)]
        public void BaseExpPerFloor_MatchesTheDesign(int layer, long expected)
        {
            double current = LevelBalance.CumulativeExp(10 * layer) / (double)LevelBalance.ExpMultiplier(layer - 1);
            double previous = layer > 1 ? LevelBalance.CumulativeExp(10 * (layer - 1)) / (double)LevelBalance.ExpMultiplier(layer - 2) : 0d;
            Assert.AreEqual(expected, (long)Math.Round((current - previous) / 5d, MidpointRounding.AwayFromZero));
        }

        [Test]
        public void OneHugeGain_LevelsUpManyTimesAndKeepsTheRemainder()
        {
            var progress = new LevelProgress(GrowthCurve.Default);
            int ups = 0, from = 0, to = 0;
            progress.LeveledUp += (_, f, t) => { ups++; from = f; to = t; };

            // Lv1 から Lv30 に着くまで（need(1)〜need(29) の合計）と 123 余り。
            long toReach30 = LevelBalance.CumulativeExp(29);
            Assert.AreEqual(29, progress.AddExp((int)toReach30 + 123));
            Assert.AreEqual(30, progress.Level);
            Assert.AreEqual(123, progress.Exp);
            Assert.AreEqual(toReach30 + 123, progress.TotalExp);
            Assert.AreEqual(1, ups, "何レベル上がっても 1 回で知らせる");
            Assert.AreEqual(1, from);
            Assert.AreEqual(30, to);
            Assert.AreEqual(LevelBalance.SoulReward(30), MetaUpgrades.SoulsFor(progress.Level), "そのレベルで全滅したときのソウル");
        }
    }
}
