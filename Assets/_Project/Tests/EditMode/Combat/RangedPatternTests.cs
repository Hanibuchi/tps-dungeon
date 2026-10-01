using System;
using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>弓の矢の向き（数）、持続弓の雨の置き場所（数）、遅れて出す回数（多重）を確かめる。</summary>
    public sealed class RangedPatternTests
    {
        [Test]
        public void 矢が0本なら空()
        {
            Assert.IsEmpty(RangedPattern.VolleyAngles(0, 8f));
            Assert.IsEmpty(RangedPattern.VolleyAngles(-1, 8f));
        }

        [Test]
        public void 一本目は必ず照準へ_増えた分は右左交互に開く()
        {
            Assert.AreEqual(new[] { 0f }, RangedPattern.VolleyAngles(1, 8f));
            Assert.AreEqual(new[] { 0f, 8f }, RangedPattern.VolleyAngles(2, 8f));
            Assert.AreEqual(new[] { 0f, 8f, -8f }, RangedPattern.VolleyAngles(3, 8f));
            Assert.AreEqual(new[] { 0f, 8f, -8f, 16f }, RangedPattern.VolleyAngles(4, 8f));
            Assert.AreEqual(new[] { 0f, 8f, -8f, 16f, -16f }, RangedPattern.VolleyAngles(5, 8f));
        }

        [Test]
        public void 雨が0個なら空()
        {
            Assert.IsEmpty(RangedPattern.RainOffsets(0, 1f, 3f, new Random(1)));
            Assert.IsEmpty(RangedPattern.RainOffsets(-1, 1f, 3f, new Random(1)));
        }

        [Test]
        public void ひとつ目は必ず狙った所()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                (float x, float z)[] offsets = RangedPattern.RainOffsets(4, 1f, 3f, new Random(seed));
                Assert.AreEqual(4, offsets.Length);
                Assert.AreEqual(0f, offsets[0].x);
                Assert.AreEqual(0f, offsets[0].z);
            }
        }

        [Test]
        public void 増えた雨は狙った所から決めた距離の範囲に置く()
        {
            var random = new Random(7);
            for (int n = 0; n < 200; n++)
            {
                (float x, float z) offset = RangedPattern.RainOffset(1.25f, 3f, random);
                float distance = (float)Math.Sqrt(offset.x * offset.x + offset.z * offset.z);
                Assert.That(distance, Is.InRange(1.25f - 1e-4f, 3f + 1e-4f));
            }
        }

        [Test]
        public void 増えた雨の置き場所は種が同じなら同じ()
        {
            Assert.AreEqual(RangedPattern.RainOffsets(3, 1f, 3f, new Random(42)), RangedPattern.RainOffsets(3, 1f, 3f, new Random(42)));
        }

        [Test]
        public void 多重が無ければ本撃だけ()
        {
            Assert.AreEqual(new[] { 0f }, RangedPattern.RepeatDelays(0, 0.5f));
            Assert.AreEqual(new[] { 0f }, RangedPattern.RepeatDelays(-2, 0.5f));
        }

        [Test]
        public void 多重のk回目はk回ぶん遅れる()
        {
            Assert.AreEqual(new[] { 0f, 0.5f, 1f, 1.5f }, RangedPattern.RepeatDelays(3, 0.5f));
        }
    }
}
