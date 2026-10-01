using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>持続弓の雨の置き場所（数）と、遅れて出す回数（多重）を確かめる。</summary>
    public sealed class RangedPatternTests
    {
        [Test]
        public void 雨が0個なら空()
        {
            Assert.IsEmpty(RangedPattern.RainOffsets(0, 3f));
            Assert.IsEmpty(RangedPattern.RainOffsets(-1, 3f));
        }

        [Test]
        public void ひとつ目は狙った所()
        {
            (float x, float z)[] offsets = RangedPattern.RainOffsets(1, 3f);
            Assert.AreEqual(1, offsets.Length);
            Assert.AreEqual(0f, offsets[0].x);
            Assert.AreEqual(0f, offsets[0].z);
        }

        [Test]
        public void 増えた雨は周りの円に前から等間隔に並ぶ()
        {
            (float x, float z)[] offsets = RangedPattern.RainOffsets(5, 2f);
            Assert.AreEqual(5, offsets.Length);
            Assert.AreEqual(0f, offsets[0].x, 1e-5f);
            Assert.AreEqual(0f, offsets[0].z, 1e-5f);

            // 前・右・後ろ・左。
            Assert.AreEqual(0f, offsets[1].x, 1e-5f);
            Assert.AreEqual(2f, offsets[1].z, 1e-5f);
            Assert.AreEqual(2f, offsets[2].x, 1e-5f);
            Assert.AreEqual(0f, offsets[2].z, 1e-5f);
            Assert.AreEqual(0f, offsets[3].x, 1e-5f);
            Assert.AreEqual(-2f, offsets[3].z, 1e-5f);
            Assert.AreEqual(-2f, offsets[4].x, 1e-5f);
            Assert.AreEqual(0f, offsets[4].z, 1e-5f);
        }

        [Test]
        public void 増えた雨がひとつなら前に置く()
        {
            (float x, float z)[] offsets = RangedPattern.RainOffsets(2, 3f);
            Assert.AreEqual(0f, offsets[1].x, 1e-5f);
            Assert.AreEqual(3f, offsets[1].z, 1e-5f);
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
