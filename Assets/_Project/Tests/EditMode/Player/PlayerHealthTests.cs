using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Player.Tests
{
    /// <summary>盾による受けるダメージの軽減。</summary>
    public sealed class PlayerHealthTests
    {
        private GameObject go;
        private PlayerHealth health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Player");
            health = go.AddComponent<PlayerHealth>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void 倍率を掛けて四捨五入で減らす()
        {
            health.DamageTaken = 100f / 130f; // 防御 30
            health.Damage(20);
            Assert.AreEqual(85, health.CurrentHp, "20 × 0.769 = 15.4 → 15");
        }

        [Test]
        public void 減らしても最低1は入る()
        {
            Assert.AreEqual(1, PlayerHealth.ReducedDamage(1, 0.2f));
            Assert.AreEqual(0, PlayerHealth.ReducedDamage(0, 0.5f));
        }

        [Test]
        public void 既定では減らない()
        {
            health.Damage(30);
            Assert.AreEqual(70, health.CurrentHp);
        }
    }
}
