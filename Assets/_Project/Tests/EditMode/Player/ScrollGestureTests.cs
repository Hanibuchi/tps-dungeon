using NUnit.Framework;

namespace TpsDungeon.Player.Tests
{
    /// <summary>
    /// なめらかスクロールを無視するとき、ホイールの値の列を 1 ノッチ 1 枠に数えられるか。
    /// 値の列は Mos でなめらかスクロールにしたマウスの実測（Editor.log に書き出したもの）から取った。
    /// </summary>
    public sealed class ScrollGestureTests
    {
        // 実測では値はおよそ 60Hz で届いていた。
        private const float Interval = 1f / 60f;

        // 1 ノッチ。0.05 から上がって山を作り、また 0.05 まで下がる。
        private static readonly float[] OneNotch =
        {
            0.05f, 0.1f, 0.15f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.15f, 0.15f, 0.15f, 0.15f,
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f,
        };

        // 続けて 3 ノッチ。前の山が下がりきる前に次の山が始まり、値は途切れない。
        private static readonly float[] ThreeNotchesInARow =
        {
            0.05f, 0.1f, 0.15f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.15f, 0.15f, 0.15f, 0.1f, 0.1f, 0.1f,
            0.1f, 0.1f, 0.1f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.1f, 0.15f, 0.2f, 0.3f,
            0.4f, 0.4f, 0.45f, 0.45f, 0.45f, 0.45f, 0.4f, 0.4f, 0.35f, 0.35f, 0.3f, 0.3f, 0.25f, 0.25f, 0.25f,
            0.2f, 0.2f, 0.2f, 0.15f, 0.15f, 0.15f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.05f, 0.05f, 0.05f, 0.05f,
            0.05f, 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.4f, 0.4f, 0.35f,
            0.35f, 0.3f, 0.3f, 0.3f, 0.25f, 0.25f, 0.2f, 0.2f,
        };

        // 速めに回したとき。山が大きく、谷も浅いうちに次の山が来る。
        private static readonly float[] FastNotches =
        {
            0.05f, 0.1f, 0.25f, 0.45f, 0.75f, 1.2f, 1.75f, 2.05f, 2.25f, 2.35f, 2.35f, 2.35f, 2.25f, 2.15f,
            2.05f, 1.95f, 1.8f, 1.7f, 1.6f, 1.45f, 1.35f, 1.25f, 1.15f, 1.05f, 1.15f, 1.45f, 1.9f, 2.3f, 2.55f,
            2.65f, 2.7f, 2.65f, 2.6f, 2.5f, 2.35f, 2.25f, 2.1f, 1.95f, 1.8f, 1.7f, 1.55f, 1.45f, 1.3f, 1.2f,
            1.3f, 1.6f, 2.05f, 2.45f, 2.65f, 2.75f, 2.8f, 2.75f, 2.65f, 2.55f, 2.4f, 2.3f, 2.15f, 2f, 1.85f,
            1.7f, 1.6f, 1.45f, 1.35f, 1.25f, 1.15f, 1.05f, 0.95f, 0.9f, 0.8f, 0.75f, 0.65f, 0.6f, 0.55f, 0.5f,
        };

        /// <summary>values を等間隔で渡し、送った枠の合計を返す。</summary>
        private static int Feed(ScrollGesture gesture, float start, params float[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++) total += gesture.Step(values[i], start + i * Interval);
            return total;
        }

        private static float[] Negate(float[] values)
        {
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = -values[i];
            return result;
        }

        [Test]
        public void ノッチ1つの値の列は1枠だけ送る()
        {
            Assert.AreEqual(1, Feed(new ScrollGesture(), 0f, OneNotch));
        }

        [Test]
        public void 続けて回したノッチは山ごとに1枠ずつ送る()
        {
            Assert.AreEqual(3, Feed(new ScrollGesture(), 0f, ThreeNotchesInARow));
        }

        [Test]
        public void 速く回して値が大きくても山ごとに1枠ずつ送る()
        {
            Assert.AreEqual(3, Feed(new ScrollGesture(), 0f, FastNotches));
        }

        [Test]
        public void 逆向きに回しても山ごとに1枠ずつ戻す()
        {
            Assert.AreEqual(-3, Feed(new ScrollGesture(), 0f, Negate(ThreeNotchesInARow)));
        }

        [Test]
        public void 重いフレームで1つだけ跳ねた値は次の山と数えない()
        {
            // 実測: 2 回分がまとめて届いた 0.35 の前後は下がり続けている。
            var gesture = new ScrollGesture();
            Assert.AreEqual(1, Feed(gesture, 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.35f, 0.15f, 0.15f, 0.15f, 0.1f, 0.1f, 0.05f));
        }

        [Test]
        public void 途切れてから回すと次の枠へ送る()
        {
            var gesture = new ScrollGesture(0.15f);
            Assert.AreEqual(1, Feed(gesture, 0f, OneNotch));
            Assert.AreEqual(1, Feed(gesture, 1f, OneNotch));
        }

        [Test]
        public void 値が0のフレームは回しの続きに数えない()
        {
            var gesture = new ScrollGesture(0.15f);
            Assert.AreEqual(1, gesture.Step(0.2f, 0f));
            for (float t = Interval; t < 0.3f; t += Interval) Assert.AreEqual(0, gesture.Step(0f, t));
            Assert.AreEqual(1, gesture.Step(0.2f, 0.3f));
        }

        [Test]
        public void 向きが変わったらすぐ反対へ送る()
        {
            var gesture = new ScrollGesture();
            Assert.AreEqual(1, Feed(gesture, 0f, 0.05f, 0.1f, 0.2f, 0.2f));
            Assert.AreEqual(-1, Feed(gesture, 4 * Interval, -0.1f, -0.2f, -0.4f, -0.5f));
        }
    }
}
