using NUnit.Framework;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>経験値の獲得、複数レベル同時アップ、上限、リセット、復元。</summary>
    public sealed class LevelProgressTests
    {
        private LevelProgress progress;
        private int levelUps;
        private int lastFrom;
        private int lastTo;

        [SetUp]
        public void SetUp()
        {
            progress = new LevelProgress(GrowthCurve.Default);
            levelUps = 0;
            progress.LeveledUp += (_, from, to) =>
            {
                levelUps++;
                lastFrom = from;
                lastTo = to;
            };
        }

        [Test]
        public void BelowThreshold_OnlyAccumulates()
        {
            Assert.AreEqual(0, progress.AddExp(9));
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual(9, progress.Exp);
            Assert.AreEqual(0, levelUps);
        }

        [Test]
        public void ReachingThreshold_LevelsUpAndCarriesOver()
        {
            Assert.AreEqual(1, progress.AddExp(15));
            Assert.AreEqual(2, progress.Level);
            Assert.AreEqual(5, progress.Exp, "Lv1→2 の 10 を引いた余り");
            Assert.AreEqual(1, levelUps);
        }

        [Test]
        public void OneBigGain_LevelsUpSeveralTimesAndNotifiesOnce()
        {
            // Lv1→5 は 10+40+90+160 = 300。
            Assert.AreEqual(4, progress.AddExp(305));
            Assert.AreEqual(5, progress.Level);
            Assert.AreEqual(5, progress.Exp);
            Assert.AreEqual(1, levelUps, "何レベル上がっても 1 回");
            Assert.AreEqual(1, lastFrom);
            Assert.AreEqual(5, lastTo);
            Assert.AreEqual(305, progress.TotalExp);
        }

        [Test]
        public void ReachingMaxLevel_DropsRemainderAndIgnoresFurtherGains()
        {
            progress.AddExp(int.MaxValue);
            Assert.AreEqual(99, progress.Level);
            Assert.AreEqual(0, progress.Exp, "上限に着いたら余りは捨てる");
            Assert.IsTrue(progress.IsMaxLevel);

            int changes = 0;
            progress.ExpChanged += _ => changes++;
            Assert.AreEqual(0, progress.AddExp(1000));
            Assert.AreEqual(0, progress.Exp, "Lv99 では経験値を足さない");
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void NonPositiveGain_IsIgnored()
        {
            Assert.AreEqual(0, progress.AddExp(0));
            Assert.AreEqual(0, progress.AddExp(-50));
            Assert.AreEqual(0, progress.Exp);
        }

        [Test]
        public void Reset_BackToLevelOne()
        {
            progress.AddExp(5000);
            progress.Reset();
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual(0, progress.Exp);
        }

        [Test]
        public void Restore_ClampsOutOfRangeValues()
        {
            progress.Restore(150, 99999);
            Assert.AreEqual(99, progress.Level);
            Assert.AreEqual(0, progress.Exp);

            progress.Restore(3, 500);
            Assert.AreEqual(3, progress.Level);
            Assert.AreEqual(89, progress.Exp, "次に届く手前（90-1）まで");

            progress.Restore(-2, -10);
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual(0, progress.Exp);
            Assert.AreEqual(0, levelUps, "復元はレベルアップとして知らせない");
        }

        [Test]
        public void Modifiers_ApplyExpExtraMultiplier()
        {
            var modifiers = new ProgressionModifiers();
            modifiers.SetExpMultiplier(1.5f);
            Assert.AreEqual(18, modifiers.ApplyExp(10, 1.2f), "1.5 × 1.2 = 1.8 倍");
            Assert.AreEqual(0, modifiers.ApplyExp(10, 0f));
        }

        [Test]
        public void Modifiers_ApplyExpMultiplier()
        {
            var modifiers = new ProgressionModifiers();
            Assert.AreEqual(10, modifiers.ApplyExp(10));

            modifiers.SetExpMultiplier(1.5f);
            Assert.AreEqual(15, modifiers.ApplyExp(10));
            Assert.AreEqual(2, modifiers.ApplyExp(1), "1.5 は四捨五入で 2");

            modifiers.SetExpMultiplier(0.1f);
            Assert.AreEqual(1, modifiers.ApplyExp(1), "倍率が正なら最低 1");

            modifiers.SetExpMultiplier(-1f);
            Assert.AreEqual(0, modifiers.ApplyExp(10));
        }
    }
}
