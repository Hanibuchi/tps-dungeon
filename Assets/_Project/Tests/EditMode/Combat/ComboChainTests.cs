using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>コンボボーナスの段数（続けて当てた段の数）の数え方を確かめる。</summary>
    public sealed class ComboChainTests
    {
        [Test]
        public void 当てるたびに増え_周をまたいでも上限なく続く()
        {
            var chain = new ComboChain();
            for (int i = 0; i < 20; i++) Assert.IsTrue(chain.SwingFinished(1));
            Assert.AreEqual(20, chain.Count);
        }

        [Test]
        public void 空振りで0に戻る()
        {
            var chain = new ComboChain();
            chain.SwingFinished(2);
            chain.SwingFinished(1);

            Assert.IsTrue(chain.SwingFinished(0));
            Assert.AreEqual(0, chain.Count);
            Assert.IsFalse(chain.SwingFinished(0), "0 のままなら変わっていない");
        }

        [Test]
        public void 時間切れで0に戻る()
        {
            var chain = new ComboChain();
            chain.SwingFinished(1);
            chain.StartTimeout(0.8f);

            Assert.IsFalse(chain.Tick(0.5f));
            Assert.AreEqual(1, chain.Count);
            Assert.IsTrue(chain.Tick(0.4f));
            Assert.AreEqual(0, chain.Count);
            Assert.IsFalse(chain.IsTimingOut);
        }

        [Test]
        public void 次の段が始まれば時間切れを取り消す()
        {
            var chain = new ComboChain();
            chain.SwingFinished(1);
            chain.StartTimeout(0.8f);
            chain.Tick(0.5f);

            chain.CancelTimeout();
            Assert.IsFalse(chain.Tick(10f));
            Assert.AreEqual(1, chain.Count);
        }

        [Test]
        public void 猶予が0ならすぐ途切れる()
        {
            var chain = new ComboChain();
            chain.SwingFinished(1);

            Assert.IsTrue(chain.StartTimeout(0f));
            Assert.AreEqual(0, chain.Count);
        }
    }
}
