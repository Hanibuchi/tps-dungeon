using NUnit.Framework;
using TpsDungeon.Hud;

namespace TpsDungeon.Player.Tests
{
    /// <summary>ダメージの数字の動き（上がる高さ・はじける大きさ・消えていく濃さ）。</summary>
    public sealed class DamageNumberViewTests
    {
        [Test]
        public void Rise_StartsAtZero_AndSlowsDownToTheTop()
        {
            Assert.AreEqual(0f, DamageNumberView.RiseAt(0f), 1e-5f);
            Assert.AreEqual(0.6f, DamageNumberView.RiseAt(1f), 1e-5f);
            // はじめ速い: 半分の時間で半分より上にいる。
            Assert.Greater(DamageNumberView.RiseAt(0.5f), 0.3f);
            Assert.AreEqual(0.6f, DamageNumberView.RiseAt(2f), 1e-5f);
        }

        [Test]
        public void Scale_PopsThenSettlesToOne()
        {
            Assert.AreEqual(1.4f, DamageNumberView.ScaleAt(0f), 1e-5f);
            Assert.AreEqual(1.2f, DamageNumberView.ScaleAt(0.05f), 1e-5f);
            Assert.AreEqual(1f, DamageNumberView.ScaleAt(0.1f), 1e-5f);
            Assert.AreEqual(1f, DamageNumberView.ScaleAt(DamageNumberView.Lifetime), 1e-5f);
        }

        [Test]
        public void ComboColor_WarmsUpAsTheComboGrows()
        {
            Assert.IsNull(DamageNumberView.ComboTierClass(2));
            Assert.IsNull(DamageNumberView.ComboTierClass(DamageNumberView.ComboHotFrom - 1));
            Assert.AreEqual("damage-number__combo--hot", DamageNumberView.ComboTierClass(DamageNumberView.ComboHotFrom));
            Assert.AreEqual("damage-number__combo--blazing", DamageNumberView.ComboTierClass(DamageNumberView.ComboBlazingFrom));
            Assert.AreEqual("damage-number__combo--blazing", DamageNumberView.ComboTierClass(99));
        }

        [Test]
        public void Opacity_StaysFull_ThenFadesOverTheLastPart()
        {
            Assert.AreEqual(1f, DamageNumberView.OpacityAt(0f), 1e-5f);
            Assert.AreEqual(1f, DamageNumberView.OpacityAt(0.6f), 1e-5f);
            Assert.AreEqual(0.5f, DamageNumberView.OpacityAt(0.8f), 1e-5f);
            Assert.AreEqual(0f, DamageNumberView.OpacityAt(1f), 1e-5f);
        }
    }
}
