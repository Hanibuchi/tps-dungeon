using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 部屋の敵の決め方（予算 → 編成タイプ → 体数（最大剰余法） → グループ）が仕様どおりで、シードで再現できることを確かめる。
    /// </summary>
    public sealed class RoomEnemyRollerTests
    {
        private const int Small = 0, Mid = 1, Big = 2;

        private static EnemySpawnRules Rules(params EnemyComposition[] compositions) => new EnemySpawnRules
        {
            Enemies = new List<EnemyKind> { new EnemyKind(Small, 1d), new EnemyKind(Mid, 2d), new EnemyKind(Big, 4d) },
            Compositions = compositions.ToList(),
            BudgetPerCell = 3d,
            MonsterHouseChance = 0.2d,
        };

        private static EnemyComposition Composition(double weight, params (int id, double share)[] shares) => new EnemyComposition
        {
            Weight = weight,
            Shares = shares.Select(s => new EnemyShare(s.id, s.share)).ToList(),
        };

        private static Dictionary<int, int> Totals(IEnumerable<EnemyGroup> groups)
        {
            var totals = new Dictionary<int, int>();
            foreach (var g in groups) totals[g.EnemyId] = (totals.TryGetValue(g.EnemyId, out int n) ? n : 0) + g.Count;
            return totals;
        }

        private static double Cost(EnemySpawnRules rules, IEnumerable<EnemyGroup> groups) =>
            groups.Sum(g => rules.Enemies.First(e => e.Id == g.EnemyId).Cost * g.Count);

        [Test]
        public void Counts_FollowBudgetShares_NotHeadcount()
        {
            // 予算 10、単価 1 の 1 種に 0.7、単価 2 の 1 種に 0.3 → 7 体と 1.5 体 → 整数部分 7 と 1、残り 1 で 2 体目は買えない。
            var rules = Rules();
            var counts = RoomEnemyRoller.Counts(rules, Composition(1d, (Small, 0.7d), (Mid, 0.3d)), 10d);
            CollectionAssert.AreEqual(new[] { (Small, 7), (Mid, 1) }, counts);
        }

        [Test]
        public void Counts_LargestRemainderAddsOnlyWhatTheLeftoverBudgetBuys()
        {
            // 予算 7、Small 0.5 → 3.5 体、Big 0.5 → 0.875 体。整数部分 3 と 0、残り 4。
            // 小数部分の大きい Big（0.875）を先に 4 で買い、残り 0 で Small（0.5）は買えない。
            var counts = RoomEnemyRoller.Counts(Rules(), Composition(1d, (Small, 0.5d), (Big, 0.5d)), 7d);
            CollectionAssert.AreEqual(new[] { (Small, 3), (Big, 1) }, counts);

            // 予算 5 なら残り 2 で Big は買えず、次の Small（2.5 体 → 2、+1）を買う。
            counts = RoomEnemyRoller.Counts(Rules(), Composition(1d, (Small, 0.5d), (Big, 0.5d)), 5d);
            CollectionAssert.AreEqual(new[] { (Small, 3) }, counts);
        }

        [Test]
        public void Counts_NormalizeSharesAndSkipUnknownOrFreeEnemies()
        {
            var rules = Rules();
            rules.Enemies.Add(new EnemyKind(9, 0d));
            // 比率の合計が 1 でなくても割って使う。表に無い種類・コスト 0 の種類は飛ばす。
            var counts = RoomEnemyRoller.Counts(rules, Composition(1d, (Small, 2d), (Mid, 2d), (7, 5d), (9, 5d)), 8d);
            CollectionAssert.AreEqual(new[] { (Small, 4), (Mid, 2) }, counts);
        }

        [Test]
        public void ZeroBudget_GivesEmptyRoom()
        {
            var rules = Rules(Composition(1d, (Small, 1d)));
            Assert.IsEmpty(RoomEnemyRoller.RollWithBudget(rules, 0d, new Random(1)));
            Assert.IsEmpty(RoomEnemyRoller.Roll(rules, 0d, new Random(1)));
        }

        [Test]
        public void NoCompositionWithWeight_GivesEmptyRoom()
        {
            var rules = Rules(Composition(0d, (Small, 1d)));
            Assert.IsEmpty(RoomEnemyRoller.RollWithBudget(rules, 20d, new Random(1)));
        }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 3)]
        [TestCase(12, 3)]
        [TestCase(13, 4)]
        public void GroupCount_IsRoundedSquareRoot(int count, int expected)
        {
            Assert.AreEqual(expected, RoomEnemyRoller.GroupCount(count));
        }

        [Test]
        public void Groups_SplitEachKindIntoAtMostRoundSqrtNonEmptyGroups()
        {
            var rules = Rules(Composition(1d, (Small, 0.5d), (Mid, 0.3d), (Big, 0.2d)));
            for (int seed = 1; seed <= 200; seed++)
            {
                double budget = 40d;
                var groups = RoomEnemyRoller.RollWithBudget(rules, budget, new Random(seed));
                var expected = RoomEnemyRoller.Counts(rules, rules.Compositions[0], budget);
                CollectionAssert.AreEquivalent(expected.ToDictionary(x => x.enemyId, x => x.count), Totals(groups), $"seed {seed}");
                Assert.IsTrue(groups.All(g => g.Count > 0), $"seed {seed}: 0 体のグループがある");
                foreach (var (enemyId, count) in expected)
                {
                    Assert.LessOrEqual(groups.Count(g => g.EnemyId == enemyId), RoomEnemyRoller.GroupCount(count), $"seed {seed}");
                }
            }
        }

        [Test]
        public void Roll_StaysWithinBudgetAndIsReproducible()
        {
            var rules = Rules(
                Composition(2d, (Small, 1d), (Mid, 1d), (Big, 1d)),
                Composition(1d, (Big, 0.6d), (Small, 0.4d)),
                Composition(0.5d, (Mid, 1d)));
            bool anyEnemy = false, anyEmpty = false;
            for (int seed = 1; seed <= 300; seed++)
            {
                var a = RoomEnemyRoller.Roll(rules, 12d, new Random(seed));
                var b = RoomEnemyRoller.Roll(rules, 12d, new Random(seed));
                CollectionAssert.AreEqual(a, b, $"seed {seed} で同じにならない");
                Assert.LessOrEqual(Cost(rules, a), 12d + 1e-9, $"seed {seed}: 上限を超えた");
                if (a.Count > 0) anyEnemy = true;
                else anyEmpty = true;
            }
            Assert.IsTrue(anyEnemy);
            Assert.IsTrue(anyEmpty, "予算は 0 からなので、空の部屋もあり得る");
        }

        [Test]
        public void PickComposition_FollowsWeights()
        {
            var heavy = Composition(3d, (Small, 1d));
            var light = Composition(1d, (Mid, 1d));
            var never = Composition(0d, (Big, 1d));
            var list = new List<EnemyComposition> { heavy, light, never };
            var rng = new Random(5);
            int heavyCount = 0, total = 4000;
            for (int i = 0; i < total; i++)
            {
                var picked = RoomEnemyRoller.PickComposition(list, rng);
                Assert.AreNotSame(never, picked);
                if (picked == heavy) heavyCount++;
            }
            Assert.AreEqual(0.75d, heavyCount / (double)total, 0.03d);
        }

        [Test]
        public void MaxBudget_IsProportionalToRoomCells()
        {
            var rules = Rules();
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 3);
            foreach (var room in layout.Rooms)
            {
                Assert.AreEqual(3d * room.Bounds.Width * room.Bounds.Height, RoomEnemyRoller.MaxBudget(rules, room), 1e-9);
            }
        }

        [Test]
        public void MaxBudget_WithMeanSide_IsProportionalToAverageSide()
        {
            var rules = Rules();
            rules.BudgetBasis = RoomBudgetBasis.MeanSide;
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 3);
            foreach (var room in layout.Rooms)
            {
                Assert.AreEqual(3d * (room.Bounds.Width + room.Bounds.Height) / 2d, RoomEnemyRoller.MaxBudget(rules, room), 1e-9);
            }
            Assert.AreEqual(2d, RoomEnemyRoller.BudgetSize(RoomBudgetBasis.MeanSide, new GridRect(0, 0, 2, 2)));
            Assert.AreEqual(4d, RoomEnemyRoller.BudgetSize(RoomBudgetBasis.Area, new GridRect(0, 0, 2, 2)));
        }

        [Test]
        public void MonsterHouse_MultipliesTheBudgetCap()
        {
            var rules = Rules(Composition(1d, (Small, 1d)));
            rules.MonsterHouseBudgetMultiplier = 2.5d;
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 3);
            foreach (var room in layout.Rooms)
            {
                Assert.AreEqual(3d * 2.5d * room.Bounds.Area, RoomEnemyRoller.MaxBudget(rules, room, monsterHouse: true), 1e-9);
            }

            // 同じ部屋・同じ種でも、倍率の分だけ多く出る（予算の引き方は同じなので、上限に比例する）。
            int normal = 0, house = 0;
            foreach (var room in layout.Rooms)
            {
                normal += RoomEnemyRoller.RollRoom(rules, layout, room.Index).Sum(g => g.Count);
                house += RoomEnemyRoller.RollRoom(rules, layout, room.Index, monsterHouse: true).Sum(g => g.Count);
            }
            Assert.Greater(house, normal);
        }

        [Test]
        public void OnlyNormalRoomsGetEnemiesOrBecomeMonsterHouses()
        {
            var rules = Rules(Composition(1d, (Small, 1d)));
            rules.BudgetPerCell = 50d;
            rules.MonsterHouseChance = 1d;
            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), seed);
                var houses = RoomEnemyRoller.PickMonsterHouses(rules, layout);
                foreach (var room in layout.Rooms)
                {
                    bool normal = room.Role == RoomRole.Normal;
                    Assert.AreEqual(normal, houses.Contains(room.Index), $"seed {seed} {room}");
                    if (!normal) Assert.IsEmpty(RoomEnemyRoller.RollRoom(rules, layout, room.Index), $"seed {seed} {room}");
                }
            }
        }

        [Test]
        public void MonsterHouseChance_IsRoughlyHonored()
        {
            var rules = Rules(Composition(1d, (Small, 1d)));
            int normal = 0, houses = 0;
            foreach (int seed in FloorGenerationFixture.Seeds(60))
            {
                var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), seed);
                normal += layout.Rooms.Count(r => r.Role == RoomRole.Normal);
                houses += RoomEnemyRoller.PickMonsterHouses(rules, layout).Count;
            }
            Assert.AreEqual(0.2d, houses / (double)normal, 0.05d);
        }

        [Test]
        public void RollRoom_DependsOnlyOnFloorSeedAndRoom()
        {
            var rules = Rules(Composition(1d, (Small, 0.5d), (Mid, 0.5d)));
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 11);
            var forward = layout.Rooms.Select(r => RoomEnemyRoller.RollRoom(rules, layout, r.Index)).ToList();
            var backward = layout.Rooms.Reverse().Select(r => RoomEnemyRoller.RollRoom(rules, layout, r.Index)).Reverse().ToList();
            for (int i = 0; i < forward.Count; i++) CollectionAssert.AreEqual(forward[i], backward[i], $"部屋 {i}");
        }
    }
}
