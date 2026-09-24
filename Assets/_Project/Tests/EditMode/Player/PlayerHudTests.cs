using NUnit.Framework;
using TpsDungeon.Hud;
using UnityEngine;

namespace TpsDungeon.Player.Tests
{
    /// <summary>HP の増減、ホットバーの選択、地図の開け閉め、HUD に出す文字。</summary>
    public sealed class PlayerHudTests
    {
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Health_ClampsBetweenZeroAndMax()
        {
            var health = go.AddComponent<PlayerHealth>();
            int changes = 0;
            health.Changed += _ => changes++;

            health.Damage(30);
            Assert.AreEqual(70, health.CurrentHp);

            health.Damage(500);
            Assert.AreEqual(0, health.CurrentHp);
            Assert.IsTrue(health.IsDead);

            health.Heal(500);
            Assert.AreEqual(health.MaxHp, health.CurrentHp);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void Health_IgnoresNonPositiveAmounts()
        {
            var health = go.AddComponent<PlayerHealth>();
            int changes = 0;
            health.Changed += _ => changes++;

            health.Damage(0);
            health.Damage(-10);
            health.Heal(-10);

            Assert.AreEqual(100, health.CurrentHp);
            Assert.AreEqual(0, changes, "値が変わらなければ知らせない");
        }

        [Test]
        public void Health_LoweringMax_TrimsCurrent()
        {
            var health = go.AddComponent<PlayerHealth>();

            health.SetMax(40);

            Assert.AreEqual(40, health.MaxHp);
            Assert.AreEqual(40, health.CurrentHp);
            Assert.AreEqual(1f, health.Fraction);
        }

        [Test]
        public void Hotbar_CycleWrapsAround()
        {
            var hotbar = go.AddComponent<PlayerHotbar>();

            hotbar.Cycle(-1);
            Assert.AreEqual(PlayerHotbar.SlotCount - 1, hotbar.SelectedIndex, "左端から左へ送ると右端に回り込む");

            hotbar.Cycle(1);
            Assert.AreEqual(0, hotbar.SelectedIndex, "右端から右へ送ると左端に回り込む");
        }

        [Test]
        public void Hotbar_SelectClampsToSlots()
        {
            var hotbar = go.AddComponent<PlayerHotbar>();

            hotbar.Select(99);
            Assert.AreEqual(PlayerHotbar.SlotCount - 1, hotbar.SelectedIndex);

            hotbar.Select(-3);
            Assert.AreEqual(0, hotbar.SelectedIndex);
        }

        [TestCase(120f, 1)]
        [TestCase(0.1f, 1)]
        [TestCase(-120f, -1)]
        [TestCase(0f, 0)]
        public void Hotbar_ScrollValueBecomesOneStep(float value, int expected)
        {
            Assert.AreEqual(expected, PlayerHotbar.ScrollStep(value));
        }

        [Test]
        public void MapToggle_OpensAndClosesAndNotifiesOnlyOnChange()
        {
            var toggle = go.AddComponent<PlayerMapToggle>();
            int changes = 0;
            toggle.Changed += _ => changes++;

            Assert.IsFalse(toggle.IsOpen, "最初は閉じている");

            toggle.Toggle();
            Assert.IsTrue(toggle.IsOpen);

            toggle.SetOpen(true);
            toggle.Toggle();
            Assert.IsFalse(toggle.IsOpen);
            Assert.AreEqual(2, changes, "開いたままの SetOpen(true) では知らせない");
        }

        [Test]
        public void Hud_HpText()
        {
            Assert.AreEqual("80 / 100", GameHudView.HpText(80, 100));
        }
    }
}
