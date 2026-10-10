using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Player.Tests
{
    /// <summary>盾と加護による受けるダメージの軽減。</summary>
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
    
        [Test]
        public void 加護の軽減は盾の倍率に掛け合わせる()
        {
            health.DamageTaken = 0.8f;
            go.AddComponent<CharacterBuffs>().Apply(BlessingKind.DamageReduction, 0.25f, 10f);
            health.Damage(50);
            Assert.AreEqual(70, health.CurrentHp, "50 × 0.8 × 0.75 = 30");
        }

        [Test]
        public void ダメージ軽減以外の加護は被ダメージに効かない()
        {
            var buffs = go.AddComponent<CharacterBuffs>();
            buffs.Apply(BlessingKind.CritMultiplier, 0.5f, 10f);
            buffs.Apply(BlessingKind.AilmentResistance, 0.4f, 10f);
            Assert.AreEqual(1f, health.EffectiveDamageTaken, 1e-5f);
            Assert.AreEqual(0.5f, buffs.CritMultiplierBonus, 1e-5f);
            Assert.AreEqual(0.4f, buffs.AilmentResistance, 1e-5f);
        }

        [Test]
        public void 加護は種類ごとに付いて外れる()
        {
            var buffs = go.AddComponent<CharacterBuffs>();
            buffs.Apply(BlessingKind.DamageReduction, 0.5f, 10f);
            buffs.Apply(BlessingKind.CritMultiplier, 0.5f, 10f);
            Assert.IsTrue(buffs.Has(BlessingKind.DamageReduction));
            Assert.IsFalse(buffs.Has(BlessingKind.AilmentResistance));

            buffs.Clear(BlessingKind.DamageReduction);
            Assert.IsFalse(buffs.Has(BlessingKind.DamageReduction));
            Assert.AreEqual(1f, health.EffectiveDamageTaken, 1e-5f);
            Assert.IsTrue(buffs.Has(BlessingKind.CritMultiplier), "ほかの種類は残る");
            Assert.AreEqual(0.5f, buffs.CritMultiplierBonus, 1e-5f);
        }

        [Test]
        public void 同じ種類の加護は重ねず_大きい方の値と長い方の残りを取る()
        {
            var buffs = go.AddComponent<CharacterBuffs>();
            buffs.Apply(BlessingKind.DamageReduction, 0.2f, 12f);
            buffs.Apply(BlessingKind.DamageReduction, 0.3f, 5f);
            Assert.AreEqual(0.7f, buffs.DamageTakenScale, 1e-5f);
            Assert.AreEqual(12f, buffs.Remaining(BlessingKind.DamageReduction), 1e-5f);
        }
    }
}
