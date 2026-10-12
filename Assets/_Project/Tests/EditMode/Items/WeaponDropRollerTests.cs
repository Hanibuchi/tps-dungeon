using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>倒した敵が落とす武器の抽選（ユニークの天井・ドロップ増加の倍率）を確かめる。</summary>
    public sealed class WeaponDropRollerTests
    {
        /// <summary>NextDouble が決めた値を順に返す。尽きたら最後の値を返し続ける。</summary>
        private sealed class FixedRandom : Random
        {
            private readonly double[] values;
            private int index;

            public FixedRandom(params double[] values) => this.values = values;

            public override double NextDouble() => values[Math.Min(index++, values.Length - 1)];
        }

        private static readonly float[] NoRanks = new float[6];

        private static float[] Ranks(float e = 0f, float d = 0f, float c = 0f, float b = 0f, float a = 0f, float s = 0f) =>
            new[] { e, d, c, b, a, s };

        [Test]
        public void ユニークが当たると数えを0に戻し_ほかの行は1足す()
        {
            var uniques = new List<WeaponDropRoller.UniqueOption> { new WeaponDropRoller.UniqueOption(0.1f, 0), new WeaponDropRoller.UniqueOption(0.5f, 0) };
            int[] misses = { 3, 7 };

            // 1 行目は 0.2 で外れ、2 行目は 0.2 で当たる。
            WeaponDropRoller.Result result = WeaponDropRoller.Roll(NoRanks, uniques, misses, 1f, new FixedRandom(0.2, 0.2));

            Assert.AreEqual(WeaponDropRoller.ResultKind.Unique, result.Kind);
            Assert.AreEqual(1, result.UniqueIndex);
            CollectionAssert.AreEqual(new[] { 4, 0 }, misses);
        }

        [Test]
        public void 天井ちょうどの回で必ず落ちる()
        {
            var uniques = new List<WeaponDropRoller.UniqueOption> { new WeaponDropRoller.UniqueOption(0f, 3) };
            int[] misses = { 0 };
            var random = new FixedRandom(0.99);

            Assert.AreEqual(WeaponDropRoller.ResultKind.None, WeaponDropRoller.Roll(NoRanks, uniques, misses, 1f, random).Kind);
            Assert.AreEqual(WeaponDropRoller.ResultKind.None, WeaponDropRoller.Roll(NoRanks, uniques, misses, 1f, random).Kind);
            Assert.AreEqual(2, misses[0]);

            WeaponDropRoller.Result third = WeaponDropRoller.Roll(NoRanks, uniques, misses, 1f, random);
            Assert.AreEqual(WeaponDropRoller.ResultKind.Unique, third.Kind, "3 回目で確定");
            Assert.AreEqual(0, misses[0]);
        }

        [Test]
        public void 天井に届いた行は確率の行より先に落ちる()
        {
            var uniques = new List<WeaponDropRoller.UniqueOption> { new WeaponDropRoller.UniqueOption(1f, 0), new WeaponDropRoller.UniqueOption(0f, 5) };
            int[] misses = { 0, 4 };

            WeaponDropRoller.Result result = WeaponDropRoller.Roll(NoRanks, uniques, misses, 1f, new FixedRandom(0.0));

            Assert.AreEqual(1, result.UniqueIndex);
            CollectionAssert.AreEqual(new[] { 1, 0 }, misses);
        }

        [Test]
        public void ユニークが出たらランクの武器は落とさず_出なければランクで振る()
        {
            var uniques = new List<WeaponDropRoller.UniqueOption> { new WeaponDropRoller.UniqueOption(0.1f, 0) };

            WeaponDropRoller.Result hit = WeaponDropRoller.Roll(Ranks(e: 1f), uniques, new int[1], 1f, new FixedRandom(0.05, 0.0));
            Assert.AreEqual(WeaponDropRoller.ResultKind.Unique, hit.Kind);

            WeaponDropRoller.Result miss = WeaponDropRoller.Roll(Ranks(e: 1f), uniques, new int[1], 1f, new FixedRandom(0.5, 0.0));
            Assert.AreEqual(WeaponDropRoller.ResultKind.Rank, miss.Kind);
            Assert.AreEqual(WeaponRank.E, miss.Rank);
        }

        [Test]
        public void ランクは確率を並べた区間で決まり_残りは何も落とさない()
        {
            float[] ranks = Ranks(e: 0.1f, d: 0.05f, s: 0.01f);

            Assert.AreEqual(WeaponRank.E, WeaponDropRoller.Roll(ranks, null, null, 1f, new FixedRandom(0.05)).Rank);
            Assert.AreEqual(WeaponRank.D, WeaponDropRoller.Roll(ranks, null, null, 1f, new FixedRandom(0.12)).Rank);
            Assert.AreEqual(WeaponRank.S, WeaponDropRoller.Roll(ranks, null, null, 1f, new FixedRandom(0.155)).Rank);
            Assert.AreEqual(WeaponDropRoller.ResultKind.None, WeaponDropRoller.Roll(ranks, null, null, 1f, new FixedRandom(0.17)).Kind);
        }

        [Test]
        public void 倍率で確率が上がる()
        {
            float[] ranks = Ranks(e: 0.1f);
            var uniques = new List<WeaponDropRoller.UniqueOption> { new WeaponDropRoller.UniqueOption(0.1f, 0) };

            Assert.AreEqual(WeaponDropRoller.ResultKind.None, WeaponDropRoller.Roll(ranks, null, null, 1f, new FixedRandom(0.15)).Kind);
            Assert.AreEqual(WeaponDropRoller.ResultKind.Rank, WeaponDropRoller.Roll(ranks, null, null, 2f, new FixedRandom(0.15)).Kind);
            Assert.AreEqual(WeaponDropRoller.ResultKind.Unique,
                WeaponDropRoller.Roll(NoRanks, uniques, new int[1], 2f, new FixedRandom(0.15)).Kind);
        }

        [Test]
        public void 倍率で合計が1を超えたら1に縮める()
        {
            float[] ranks = Ranks(e: 0.5f, d: 0.5f);

            // ×2 で 1 + 1 → 半分ずつに縮む。
            Assert.AreEqual(WeaponRank.E, WeaponDropRoller.Roll(ranks, null, null, 2f, new FixedRandom(0.49)).Rank);
            Assert.AreEqual(WeaponRank.D, WeaponDropRoller.Roll(ranks, null, null, 2f, new FixedRandom(0.51)).Rank);
            Assert.AreEqual(WeaponRank.D, WeaponDropRoller.Roll(ranks, null, null, 2f, new FixedRandom(0.9999999999)).Rank);
        }

        [Test]
        public void 確率が全部0なら何も落とさない()
        {
            Assert.AreEqual(WeaponDropRoller.ResultKind.None, WeaponDropRoller.Roll(NoRanks, null, null, 3f, new FixedRandom(0.0)).Kind);
        }
    }
}
