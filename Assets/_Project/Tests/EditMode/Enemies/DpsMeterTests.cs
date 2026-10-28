using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>カカシの DPS の数え方を確かめる。</summary>
    public sealed class DpsMeterTests
    {
        [Test]
        public void 当たる前はDPSが0()
        {
            var meter = new DpsMeter();
            Assert.IsFalse(meter.HasHits);
            Assert.AreEqual(0f, meter.Dps(10f));
            Assert.AreEqual(0f, meter.RecentDps(10f));
        }

        [Test]
        public void 計測中は最初の当たりから今までで割る()
        {
            var meter = new DpsMeter(3f, 5f);
            meter.Record(10f, 50);
            meter.Record(11f, 50, true);

            Assert.IsTrue(meter.IsMeasuring(12f));
            Assert.AreEqual(100, meter.Total);
            Assert.AreEqual(2, meter.Hits);
            Assert.AreEqual(1, meter.Criticals);
            Assert.AreEqual(50f, meter.Dps(12f), 1e-3f); // 100 ÷ 2 秒
        }

        [Test]
        public void 止めたあとは最後の当たりの1間隔あとまでで割る()
        {
            var meter = new DpsMeter(3f, 5f);
            // 2 秒ごとに 20（DPS 10）を 4 回。
            for (int i = 0; i < 4; i++) meter.Record(10f + 2f * i, 20);

            Assert.IsFalse(meter.IsMeasuring(30f));
            Assert.AreEqual(8f, meter.Elapsed(30f), 1e-4f); // 6 秒 ＋ 平均の間隔 2 秒
            Assert.AreEqual(10f, meter.Dps(30f), 1e-3f);
        }

        [Test]
        public void 当たり1回で止めたらDPSは0()
        {
            var meter = new DpsMeter(3f, 5f);
            meter.Record(10f, 99);
            Assert.AreEqual(0f, meter.Dps(20f));
            Assert.AreEqual(99, meter.Total);
        }

        [Test]
        public void 間が空いたあとの当たりで計測をやり直す()
        {
            var meter = new DpsMeter(3f, 5f);
            meter.Record(10f, 100);
            meter.Record(11f, 100);
            meter.Record(20f, 7);

            Assert.AreEqual(7, meter.Total);
            Assert.AreEqual(1, meter.Hits);
            Assert.AreEqual(20f, meter.FirstHitTime);
            Assert.AreEqual(7, meter.MaxHit);
        }

        [Test]
        public void 直近のDPSは窓の中の量を窓の長さで割る()
        {
            var meter = new DpsMeter(100f, 5f);
            meter.Record(1f, 1000);
            meter.Record(7f, 30);
            meter.Record(9f, 20);

            Assert.AreEqual(10f, meter.RecentDps(10f), 1e-3f); // 50 ÷ 5 秒（1 秒の当たりは窓の外）
        }

        [Test]
        public void 数え直すと何も無かったことになる()
        {
            var meter = new DpsMeter();
            meter.Record(1f, 10);
            meter.Reset();

            Assert.IsFalse(meter.HasHits);
            Assert.AreEqual(0, meter.Total);
            Assert.AreEqual(0f, meter.RecentDps(1f));
        }

        [Test]
        public void ダメージが0以下なら数えない()
        {
            var meter = new DpsMeter();
            meter.Record(1f, 0);
            meter.Record(1f, -5);
            Assert.IsFalse(meter.HasHits);
        }
    }
}
