using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    public sealed class FloorLayoutGeneratorTests
    {
        private const int SeedSweepCount = 100;

        [Test]
        public void SameSeed_ProducesIdenticalLayout()
        {
            var parameters = FloorGenerationFixture.Params();

            var first = FloorLayoutGenerator.Generate(parameters, 12345);
            var second = FloorLayoutGenerator.Generate(parameters, 12345);

            Assert.AreEqual(first.ComputeSignature(), second.ComputeSignature());
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentLayouts()
        {
            var parameters = FloorGenerationFixture.Params();
            var signatures = new HashSet<string>();

            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                signatures.Add(FloorLayoutGenerator.Generate(parameters, seed).ComputeSignature());
            }

            Assert.AreEqual(20, signatures.Count, "異なるシードで同じレイアウトが出ている");
        }

        [Test]
        public void Rooms_StayInsideGridAndNeverOverlap()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                for (int i = 0; i < layout.Rooms.Count; i++)
                {
                    var bounds = layout.Rooms[i].Bounds;
                    Assert.IsTrue(bounds.X >= 0 && bounds.Y >= 0
                                  && bounds.MaxX < layout.Width && bounds.MaxY < layout.Height,
                        $"seed {seed}: 部屋 {i} {bounds} がフロア外にはみ出している");

                    for (int j = i + 1; j < layout.Rooms.Count; j++)
                    {
                        Assert.IsFalse(bounds.Overlaps(layout.Rooms[j].Bounds),
                            $"seed {seed}: 部屋 {i} と {j} が重なっている");
                    }
                }
            }
        }

        [Test]
        public void Corridors_NeverRunThroughRooms()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                foreach (var cell in layout.CorridorCells)
                {
                    foreach (var room in layout.Rooms)
                    {
                        Assert.IsFalse(room.Bounds.Contains(cell),
                            $"seed {seed}: 廊下セル {cell} が部屋 {room.Index} の内側を通っている");
                    }
                }
            }
        }

        [Test]
        public void EveryRoom_IsReachableFromTheEntranceStair()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                var reachable = FloorLayoutAnalysis.ReachableRooms(layout, layout.StairUpRoom);

                var unreachable = Enumerable.Range(0, reachable.Length).Where(i => !reachable[i]).ToArray();
                Assert.IsEmpty(unreachable, $"seed {seed}: 上り階段から到達できない部屋 {string.Join(",", unreachable)}");
            }
        }

        [Test]
        public void StairsAndShop_AreDistinctAndPresent()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                Assert.GreaterOrEqual(layout.StairUpRoom, 0, $"seed {seed}: 上り階段が無い");
                Assert.GreaterOrEqual(layout.StairDownRoom, 0, $"seed {seed}: 下り階段が無い");
                Assert.AreNotEqual(layout.StairUpRoom, layout.StairDownRoom, $"seed {seed}: 階段が同じ部屋にある");

                Assert.GreaterOrEqual(layout.ShopRoom, 0, $"seed {seed}: ショップが無い");
                Assert.AreNotEqual(layout.ShopRoom, layout.StairUpRoom, $"seed {seed}: ショップが上り階段と同じ部屋");
                Assert.AreNotEqual(layout.ShopRoom, layout.StairDownRoom, $"seed {seed}: ショップが下り階段と同じ部屋");

                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.Shop), $"seed {seed}: ショップ役割の部屋が 1 つでない");
                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.StairUp), $"seed {seed}: 上り階段役割の部屋が 1 つでない");
                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.StairDown), $"seed {seed}: 下り階段役割の部屋が 1 つでない");
            }
        }

        [Test]
        public void IncludeShopFalse_LeavesNoShop()
        {
            var parameters = FloorGenerationFixture.Params();
            parameters.IncludeShop = false;

            var layout = FloorLayoutGenerator.Generate(parameters, 1);

            Assert.AreEqual(-1, layout.ShopRoom);
            Assert.IsEmpty(layout.Rooms.Where(r => r.Role == RoomRole.Shop));
        }

        /// <summary>
        /// 廊下が通った辺は、必ず両端の部屋のソケットを 1 つずつ消費している。
        /// ソケットが枯渇したときのフォールバックが壊れると、ここで records が食い違う。
        /// </summary>
        [Test]
        public void CarvedEdges_ConsumeOneSocketOnEachSide()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                foreach (var edge in layout.Edges.Where(e => e.IsCarved))
                {
                    Assert.IsNotNull(edge.SocketA, $"seed {seed}: 辺 {edge.Index} の A 側ソケットが未記録");
                    Assert.IsNotNull(edge.SocketB, $"seed {seed}: 辺 {edge.Index} の B 側ソケットが未記録");
                    Assert.AreEqual(edge.Index, edge.SocketA.UsedByEdge);
                    Assert.AreEqual(edge.Index, edge.SocketB.UsedByEdge);
                }

                int doorCount = layout.Rooms.Sum(r => r.UsedSockets().Count());
                Assert.AreEqual(layout.Edges.Count(e => e.IsCarved) * 2, doorCount,
                    $"seed {seed}: ドア数が廊下の本数と合わない");
            }
        }

        [Test]
        public void RoomCount_StaysInAWorkableRange()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                Assert.That(layout.Rooms.Count, Is.InRange(6, 16), $"seed {seed}: 部屋数 {layout.Rooms.Count} が想定外");
            }
        }
    }
}
