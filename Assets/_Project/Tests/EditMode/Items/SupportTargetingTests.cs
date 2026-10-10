using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>ダメージ軽減・治癒で掛ける相手の選び方を確かめる。</summary>
    public sealed class SupportTargetingTests
    {
        private static SupportCandidate Fresh() => new SupportCandidate { Fresh = true };
        private static SupportCandidate Blessed(float remaining) => new SupportCandidate { Fresh = false, Remaining = remaining };

        [Test]
        public void まだ掛かっていない候補からだけ選ぶ()
        {
            var candidates = new[] { Blessed(5f), Fresh(), Blessed(1f), Fresh() };
            var results = new List<int>();
            for (int seed = 0; seed < 20; seed++)
            {
                SupportTargeting.Pick(candidates, 2, true, new Random(seed), results);
                CollectionAssert.AreEquivalent(new[] { 1, 3 }, results);
            }
        }

        [Test]
        public void まだの候補はランダムに選ぶ()
        {
            var candidates = new[] { Fresh(), Fresh(), Fresh(), Fresh() };
            var seen = new HashSet<int>();
            var results = new List<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                SupportTargeting.Pick(candidates, 1, true, new Random(seed), results);
                Assert.AreEqual(1, results.Count);
                seen.Add(results[0]);
            }

            Assert.AreEqual(4, seen.Count, "どの候補も選ばれうる");
        }

        [Test]
        public void 足りない分は残り時間の短い順に掛け直す()
        {
            var candidates = new[] { Blessed(5f), Fresh(), Blessed(1f), Blessed(3f) };
            var results = new List<int>();
            SupportTargeting.Pick(candidates, 3, true, new Random(0), results);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, results);
        }

        [Test]
        public void 掛け直さないときは足りなくても足さない()
        {
            var candidates = new[] { Blessed(5f), Fresh(), Blessed(1f) };
            var results = new List<int>();
            SupportTargeting.Pick(candidates, 3, false, new Random(0), results);
            CollectionAssert.AreEqual(new[] { 1 }, results);
        }

        [Test]
        public void 人数は候補の数を超えず_同じ候補を2度選ばない()
        {
            var candidates = new[] { Fresh(), Blessed(2f) };
            var results = new List<int>();
            SupportTargeting.Pick(candidates, 5, true, new Random(0), results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);

            SupportTargeting.Pick(Array.Empty<SupportCandidate>(), 3, true, new Random(0), results);
            Assert.IsEmpty(results);
        }
    
        private static SupportCandidate Hurt(int hp, int max) => new SupportCandidate { HealthFraction = (float)hp / max, Health = hp };

        [Test]
        public void 治癒は体力の割合の低い順に選ぶ()
        {
            var candidates = new[] { Hurt(80, 100), Hurt(30, 100), Hurt(50, 200), Hurt(100, 100) };
            var results = new List<int>();
            SupportTargeting.PickMostHurt(candidates, 2, results);
            CollectionAssert.AreEqual(new[] { 2, 1 }, results, "25% → 30% の順");
        }

        [Test]
        public void 治癒は割合が同じなら体力の少ない方を先に選ぶ()
        {
            var candidates = new[] { Hurt(100, 200), Hurt(50, 100), Hurt(60, 120) };
            var results = new List<int>();
            SupportTargeting.PickMostHurt(candidates, 3, results);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, results);
        }

        [Test]
        public void 治癒は全員満タンでも選ぶ_人数は候補の数まで()
        {
            var candidates = new[] { Hurt(100, 100), Hurt(120, 120) };
            var results = new List<int>();
            SupportTargeting.PickMostHurt(candidates, 5, results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);

            SupportTargeting.PickMostHurt(Array.Empty<SupportCandidate>(), 2, results);
            Assert.IsEmpty(results);
        }
    }
}
