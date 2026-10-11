using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>飛びつきの放物線、爆発の減り方、遠距離の立ち位置の決まり。</summary>
    public sealed class EnemyAttackRulesTests
    {
        [Test]
        public void LeapHeightPeaksAtMiddleAndIsZeroAtEnds()
        {
            Assert.AreEqual(0f, EnemyAttackRules.LeapHeight(0f, 1.5f), 1e-5f);
            Assert.AreEqual(1.5f, EnemyAttackRules.LeapHeight(0.5f, 1.5f), 1e-5f);
            Assert.AreEqual(0f, EnemyAttackRules.LeapHeight(1f, 1.5f), 1e-5f);
            Assert.AreEqual(EnemyAttackRules.LeapHeight(0.25f, 2f), EnemyAttackRules.LeapHeight(0.75f, 2f), 1e-5f, "左右対称");
            Assert.AreEqual(0f, EnemyAttackRules.LeapHeight(1.4f, 2f), 1e-5f, "範囲の外は端に揃える");
        }

        [Test]
        public void LeapDurationIsDistanceOverSpeed_Clamped()
        {
            Assert.AreEqual(0.5f, EnemyAttackRules.LeapDuration(5f, 10f, 0.3f, 0.8f), 1e-5f);
            Assert.AreEqual(0.3f, EnemyAttackRules.LeapDuration(0.5f, 10f, 0.3f, 0.8f), 1e-5f, "近くても短すぎない");
            Assert.AreEqual(0.8f, EnemyAttackRules.LeapDuration(20f, 10f, 0.3f, 0.8f), 1e-5f, "遠くても長すぎない");
        }

        [Test]
        public void ExplosionDamageFallsOffToEdgeScale()
        {
            Assert.AreEqual(40, EnemyAttackRules.ExplosionDamage(40, 0f, 4f, 0.25f), "爆心はそのまま");
            Assert.AreEqual(25, EnemyAttackRules.ExplosionDamage(40, 2f, 4f, 0.25f), "半分の距離で中間");
            Assert.AreEqual(10, EnemyAttackRules.ExplosionDamage(40, 4f, 4f, 0.25f), "縁で edgeScale 倍");
            Assert.AreEqual(0, EnemyAttackRules.ExplosionDamage(40, 4.01f, 4f, 0.25f), "外は 0");
            Assert.AreEqual(1, EnemyAttackRules.ExplosionDamage(2, 4f, 4f, 0f), "範囲内なら最低 1");
        }

        [Test]
        public void RangedStanceDependsOnSightAndDistance()
        {
            Assert.AreEqual(RangedStance.Approach, EnemyAttackRules.Stance(6f, false, 3f, 12f), "見えていなければ寄る");
            Assert.AreEqual(RangedStance.Retreat, EnemyAttackRules.Stance(2f, true, 3f, 12f));
            Assert.AreEqual(RangedStance.Hold, EnemyAttackRules.Stance(6f, true, 3f, 12f));
            Assert.AreEqual(RangedStance.Hold, EnemyAttackRules.Stance(12f, true, 3f, 12f), "射程ちょうどは撃てる");
            Assert.AreEqual(RangedStance.Approach, EnemyAttackRules.Stance(13f, true, 3f, 12f));
        }
    }
}
