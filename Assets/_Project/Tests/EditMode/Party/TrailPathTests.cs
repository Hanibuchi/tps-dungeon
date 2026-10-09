using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Party.Tests
{
    /// <summary>先頭が通った跡から、跡に沿って何メートル後ろの点を出す規則を確かめる。</summary>
    public sealed class TrailPathTests
    {
        private const float Tolerance = 1e-3f;

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance), $"期待 {expected} 実際 {actual}");
        }

        [Test]
        public void 作り直した直後は後ろの向きにまっすぐ並ぶ()
        {
            var trail = new TrailPath(0.5f, 20f);
            trail.Reset(new Vector3(1f, 0f, 2f), Vector3.back, 10f);

            AssertNear(new Vector3(1f, 0f, 2f), trail.PointAt(0f));
            AssertNear(new Vector3(1f, 0f, -1f), trail.PointAt(3f));
            Assert.AreEqual(10f, trail.Length, Tolerance);
        }

        [Test]
        public void 作り直した跡は指定の長さより後ろへ延ばさない()
        {
            var trail = new TrailPath(0.5f, 20f);
            trail.Reset(Vector3.zero, Vector3.back, 1.2f);

            AssertNear(new Vector3(0f, 0f, -1.2f), trail.PointAt(5f));
            Assert.AreEqual(1.2f, trail.Length, Tolerance);
        }

        [Test]
        public void まっすぐ進めば通った道の上に並ぶ()
        {
            var trail = new TrailPath(0.5f, 20f);
            trail.Reset(Vector3.zero, Vector3.back, 0f);
            for (int i = 1; i <= 20; i++) trail.Record(new Vector3(0f, 0f, i * 0.5f));

            AssertNear(new Vector3(0f, 0f, 10f), trail.PointAt(0f));
            AssertNear(new Vector3(0f, 0f, 8.4f), trail.PointAt(1.6f));
            AssertNear(new Vector3(0f, 0f, 6.8f), trail.PointAt(3.2f));
        }

        [Test]
        public void 角を曲がると後ろの点は角の手前の道に残る()
        {
            var trail = new TrailPath(0.25f, 20f);
            trail.Reset(Vector3.zero, Vector3.back, 0f);
            for (int i = 1; i <= 16; i++) trail.Record(new Vector3(0f, 0f, i * 0.25f));   // 北へ 4 m
            for (int i = 1; i <= 8; i++) trail.Record(new Vector3(i * 0.25f, 0f, 4f));    // 東へ 2 m

            // 2 m 後ろは角、3 m 後ろは北へ向かう道の上。
            AssertNear(new Vector3(0f, 0f, 4f), trail.PointAt(2f));
            AssertNear(new Vector3(0f, 0f, 3f), trail.PointAt(3f));
        }

        [Test]
        public void 一度に大きく動いても間を埋めて跡を残す()
        {
            var trail = new TrailPath(0.5f, 20f);
            trail.Reset(Vector3.zero, Vector3.back, 0f);
            trail.Record(new Vector3(3f, 0f, 0f));

            AssertNear(new Vector3(1.5f, 0f, 0f), trail.PointAt(1.5f));
        }

        [Test]
        public void 跡より後ろは一番古い点に留める()
        {
            var trail = new TrailPath(0.5f, 2f);
            trail.Reset(Vector3.zero, Vector3.back, 0f);
            for (int i = 1; i <= 40; i++) trail.Record(new Vector3(0f, 0f, i * 0.5f));

            // 残っている跡は 6 点（2.5 m）だけ。それより後ろはその端に留まる。
            AssertNear(new Vector3(0f, 0f, 17.5f), trail.PointAt(10f));
            Assert.AreEqual(Vector3.back, trail.TailDirection);
        }

        [Test]
        public void 間隔より短い動きでは点を足さない()
        {
            var trail = new TrailPath(0.5f, 20f);
            trail.Reset(Vector3.zero, Vector3.back, 0f);
            trail.Record(new Vector3(0f, 0f, 0.2f));

            Assert.AreEqual(1, trail.Count);
            AssertNear(new Vector3(0f, 0f, 0.2f), trail.Head);
        }
    }
}
