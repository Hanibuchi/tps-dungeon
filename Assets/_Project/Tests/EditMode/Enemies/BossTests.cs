using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>ボスの段階（BossPhaseRules）とボスの表（Bosses.csv）を確かめる。</summary>
    public sealed class BossTests
    {
        [TestCase(1f, 1)]
        [TestCase(0.67f, 1)]
        [TestCase(0.66f, 2)]
        [TestCase(0.34f, 2)]
        [TestCase(0.33f, 3)]
        [TestCase(0f, 3)]
        public void 残りHPの割合で段階が決まる(float fraction, int stage)
        {
            Assert.AreEqual(stage, BossPhaseRules.StageFor(fraction, 0.66f, 0.33f));
        }

        [Test]
        public void テンポは段階1で1_上の段階ほど速い()
        {
            Assert.AreEqual(1f, BossPhaseRules.TempoFor(1, 1.2f, 1.5f));
            Assert.AreEqual(1.2f, BossPhaseRules.TempoFor(2, 1.2f, 1.5f));
            Assert.AreEqual(1.5f, BossPhaseRules.TempoFor(3, 1.2f, 1.5f));
        }

        [Test]
        public void ボスの表を読む()
        {
            const string text = "# メモ\nid,layer,name,phase2_hp,phase3_hp,tempo2,tempo3\nboss_l1,1,蒼鬼の門番,0.66,0.33,1.2,1.45\n";
            var errors = new List<string>();
            List<BossCsv.Row> rows = BossCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("boss_l1", rows[0].Id);
            Assert.AreEqual(1, rows[0].Layer);
            Assert.AreEqual("蒼鬼の門番", rows[0].Name);
            Assert.AreEqual(0.66f, rows[0].Phase2Hp, 1e-6f);
            Assert.AreEqual(1.45f, rows[0].Tempo3, 1e-6f);
            Assert.AreEqual("", rows[0].Bgm);
        }

        [Test]
        public void bgm列は任意_書けばパスを読む()
        {
            const string text = "id,layer,name,phase2_hp,phase3_hp,tempo2,tempo3,bgm\n" +
                                "a,1,A,0.66,0.33,1.2,1.4,DOVA-SYNDROME/ボス戦1.mp3\n" +
                                "b,2,B,0.66,0.33,1.2,1.4,\n";
            var errors = new List<string>();
            List<BossCsv.Row> rows = BossCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual("DOVA-SYNDROME/ボス戦1.mp3", rows[0].Bgm);
            Assert.AreEqual("", rows[1].Bgm);
        }

        [Test]
        public void 段階の境目が逆_同じ層に2体_は飛ばしてエラー()
        {
            const string text = "id,layer,name,phase2_hp,phase3_hp,tempo2,tempo3\n" +
                                "a,1,A,0.3,0.6,1.2,1.4\n" +
                                "b,2,B,0.66,0.33,1.2,1.4\n" +
                                "c,2,C,0.66,0.33,1.2,1.4\n";
            var errors = new List<string>();
            List<BossCsv.Row> rows = BossCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("b", rows[0].Id);
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("2 行目", errors[0]);
            StringAssert.Contains("4 行目", errors[1]);
        }

        [Test]
        public void kind_spawner_を読む()
        {
            Assert.IsTrue(EnemyCsv.TryParseKind("spawner", out EnemyKind kind));
            Assert.AreEqual(EnemyKind.Spawner, kind);
        }
    }
}
