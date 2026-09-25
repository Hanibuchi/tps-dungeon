using NUnit.Framework;

namespace TpsDungeon.Player.Tests
{
    /// <summary>なめらかスクロールを無視するとき、ホイールの値の列を 1 回しにまとめられるか。</summary>
    public sealed class ScrollGestureTests
    {
        private const float Frame = 1f / 60f;

        /// <summary>values を 1 フレームずつ渡し、送った枠の合計を返す。</summary>
        private static int Feed(ScrollGesture gesture, float start, params float[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++) total += gesture.Step(values[i], start + i * Frame);
            return total;
        }

        [Test]
        public void なめらかスクロールの値の列は1枠だけ送る()
        {
            // Mos は 1 ノッチを小さくなっていく値の列にして送ってくる。途中で値の来ないフレームもある。
            var gesture = new ScrollGesture();
            Assert.AreEqual(1, Feed(gesture, 0f, 8f, 6f, 4.5f, 0f, 3f, 2f, 1f, 0.5f, 0f, 0.2f, 0.1f));
        }

        [Test]
        public void 途切れてから回すと次の枠へ送る()
        {
            var gesture = new ScrollGesture(0.15f);
            Assert.AreEqual(1, gesture.Step(5f, 0f));
            Assert.AreEqual(0, gesture.Step(2f, 0.1f), "間が短ければ同じ回し");
            Assert.AreEqual(1, gesture.Step(5f, 0.3f), "間が空いたら新しい回し");
        }

        [Test]
        public void 値が0のフレームは回しの続きに数えない()
        {
            var gesture = new ScrollGesture(0.15f);
            Assert.AreEqual(1, gesture.Step(5f, 0f));
            for (float t = Frame; t < 0.3f; t += Frame) Assert.AreEqual(0, gesture.Step(0f, t));
            Assert.AreEqual(1, gesture.Step(5f, 0.3f));
        }

        [Test]
        public void 向きが変わったらすぐ反対へ送る()
        {
            var gesture = new ScrollGesture();
            Assert.AreEqual(1, Feed(gesture, 0f, 5f, 3f));
            Assert.AreEqual(-1, Feed(gesture, 2 * Frame, -5f, -3f, -1f));
        }
    }
}
