using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>敵の HP と、スタン・気絶の重なりと時間。</summary>
    public sealed class EnemyDamageTests
    {
        private GameObject go;
        private EnemyHealth health;
        private EnemyDamageReaction reaction;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Enemy");
            health = go.AddComponent<EnemyHealth>();
            reaction = go.AddComponent<EnemyDamageReaction>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TakeDamage_ReducesAndDiesOnce()
        {
            int damaged = 0, died = 0, lastDealt = 0;
            health.Damaged += (_, __, dealt) => { damaged++; lastDealt = dealt; };
            health.Died += _ => died++;

            Assert.AreEqual(30, health.CurrentHp);
            Assert.AreEqual(10, health.TakeDamage(10));
            Assert.AreEqual(20, health.CurrentHp);

            Assert.AreEqual(20, health.TakeDamage(50), "残り HP までしか減らない");
            Assert.AreEqual(20, lastDealt);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0, health.TakeDamage(10), "倒れたあとは受けない");

            Assert.AreEqual(2, damaged);
            Assert.AreEqual(1, died);
        }

        [Test]
        public void SetMaxHp_RaisesMaxAndRefills()
        {
            health.TakeDamage(10);
            health.SetMaxHp(5000);

            Assert.AreEqual(5000, health.MaxHp);
            Assert.AreEqual(5000, health.CurrentHp);

            health.SetMaxHp(0);
            Assert.AreEqual(1, health.MaxHp, "最低 1");
        }

        [Test]
        public void Stun_LastsItsDuration()
        {
            Assert.IsTrue(reaction.Apply(DamageReaction.Stun));
            Assert.IsTrue(reaction.IsStunned);
            Assert.IsFalse(reaction.CanAct);

            reaction.Tick(0.4f);
            Assert.IsTrue(reaction.IsStunned);
            reaction.Tick(0.2f);
            Assert.IsFalse(reaction.IsStunned);
            Assert.IsTrue(reaction.CanAct);
        }

        [Test]
        public void StunAgain_RestartsTimer()
        {
            reaction.Apply(DamageReaction.Stun);
            reaction.Tick(0.4f);
            reaction.Apply(DamageReaction.Stun);
            reaction.Tick(0.4f);
            Assert.IsTrue(reaction.IsStunned, "2 回目から 0.5 秒");
        }

        [Test]
        public void FaintDuringStun_Upgrades()
        {
            reaction.Apply(DamageReaction.Stun);
            Assert.IsTrue(reaction.Apply(DamageReaction.Faint));
            Assert.IsFalse(reaction.IsStunned);
            Assert.IsTrue(reaction.IsFainted);
        }

        [Test]
        public void WhileFainted_NothingStacksAndItEndsOnTime()
        {
            int recovered = 0;
            reaction.Recovered += _ => recovered++;

            reaction.Apply(DamageReaction.Faint);
            reaction.Tick(2.5f);
            Assert.IsFalse(reaction.Apply(DamageReaction.Faint), "延長しない");
            Assert.IsFalse(reaction.Apply(DamageReaction.Stun));

            reaction.Tick(0.6f);
            Assert.IsFalse(reaction.IsFainted);
            Assert.IsTrue(reaction.CanAct);
            Assert.AreEqual(1, recovered);
        }

        [Test]
        public void Dead_CannotBeStunned()
        {
            health.TakeDamage(1000);
            Assert.IsFalse(reaction.Apply(DamageReaction.Stun));
            Assert.IsFalse(reaction.CanAct);
        }
    }
}
