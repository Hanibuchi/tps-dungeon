using NUnit.Framework;

namespace TpsDungeon.Party.Tests
{
    /// <summary>並べ直すとき、真後ろの余地に入りきらなければ間隔を詰めて収めることを確かめる。</summary>
    public sealed class FormationSpacingTests
    {
        [Test]
        public void EnoughRoom_KeepsTheUsualSpacing()
        {
            Assert.AreEqual(1.6f, Party.PackedSpacing(1.6f, 8f, 4), 0.0001f);
        }

        [Test]
        public void ShortRoom_PacksEveryoneBehind()
        {
            // 階段の途中から始まって、真後ろに 1.8 m しかないとき。4 人を 0.45 m おきに収める。
            Assert.AreEqual(0.45f, Party.PackedSpacing(1.6f, 1.8f, 4), 0.0001f);
        }

        [Test]
        public void NoFollowers_IsUnchanged()
        {
            Assert.AreEqual(1.6f, Party.PackedSpacing(1.6f, 0f, 0), 0.0001f);
        }
    }
}
