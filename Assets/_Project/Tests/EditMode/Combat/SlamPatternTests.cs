using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>叩きつけの衝撃波（数）と追撃（多重）の並べ方を確かめる。</summary>
    public sealed class SlamPatternTests
    {
        [Test]
        public void 衝撃波が無ければ空()
        {
            Assert.IsEmpty(SlamPattern.ShockwaveAngles(0, 20f));
            Assert.IsEmpty(SlamPattern.ShockwaveAngles(-1, 20f));
        }

        [Test]
        public void 衝撃波1本は真っ直ぐ前()
        {
            Assert.AreEqual(new[] { 0f }, SlamPattern.ShockwaveAngles(1, 20f));
        }

        [Test]
        public void 衝撃波は前を中心に左右対称に広がる()
        {
            Assert.AreEqual(new[] { -10f, 10f }, SlamPattern.ShockwaveAngles(2, 20f));
            Assert.AreEqual(new[] { -20f, 0f, 20f }, SlamPattern.ShockwaveAngles(3, 20f));
            Assert.AreEqual(new[] { -30f, -10f, 10f, 30f }, SlamPattern.ShockwaveAngles(4, 20f));
        }

        [Test]
        public void 追撃が無ければ空()
        {
            Assert.IsEmpty(SlamPattern.FollowUps(0, 1.5f, 0.2f));
        }

        [Test]
        public void 追撃はk回目ほど前へ_遅れて落ちる()
        {
            SlamFollowUp[] followUps = SlamPattern.FollowUps(3, 1.5f, 0.2f);

            Assert.AreEqual(3, followUps.Length);
            for (int k = 1; k <= 3; k++)
            {
                Assert.AreEqual(1.5f * k, followUps[k - 1].ForwardOffset, 1e-5f);
                Assert.AreEqual(0.2f * k, followUps[k - 1].Delay, 1e-5f);
            }
        }

        [Test]
        public void 追撃のずらしと間隔は負にならない()
        {
            SlamFollowUp followUp = SlamPattern.FollowUps(1, -1f, -1f)[0];
            Assert.AreEqual(0f, followUp.ForwardOffset);
            Assert.AreEqual(0f, followUp.Delay);
        }
    }
}
