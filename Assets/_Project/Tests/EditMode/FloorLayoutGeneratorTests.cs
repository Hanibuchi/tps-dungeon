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
        public void RoomCount_LandsInTheConfiguredRange()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                Assert.That(layout.Rooms.Count,
                    Is.InRange(parameters.MinRoomCount, parameters.MaxRoomCount),
                    $"seed {seed}: 部屋数 {layout.Rooms.Count} が設定した範囲から外れている");
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

                for (int i = 0; i < layout.Rooms.Count; i++)
                {
                    Assert.IsTrue(reachable[i], $"seed {seed}: 部屋 {i} に上り階段から辿り着けない");
                }
            }
        }

        /// <summary>
        /// 成長パッキングは「木 + 追加ドア」なので、辺の本数は部屋数 - 1 以上、
        /// かつ追加ドアの上限を足した数以下に収まる。ここが崩れたら木構造が壊れている。
        /// </summary>
        [Test]
        public void EdgeCount_StaysBetweenATreeAndTheExtraDoorBudget()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                Assert.That(layout.Edges.Count,
                    Is.InRange(layout.Rooms.Count - 1, layout.Rooms.Count - 1 + parameters.ExtraDoorCount),
                    $"seed {seed}: ドア {layout.Edges.Count} 枚が部屋 {layout.Rooms.Count} に対して不自然");
            }
        }

        /// <summary>
        /// 1 つの辺は、向かい合った 2 つのソケットをちょうど 1 つずつ消費する。
        /// 部屋 2 つは隣のセル同士なので、辺を挟んで必ず接している。
        /// </summary>
        [Test]
        public void EveryEdge_JoinsTwoRoomsThroughFacingSockets()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                foreach (var edge in layout.Edges)
                {
                    Assert.IsNotNull(edge.SocketA, $"seed {seed}: 辺 {edge.Index} の A 側ソケットが未記録");
                    Assert.IsNotNull(edge.SocketB, $"seed {seed}: 辺 {edge.Index} の B 側ソケットが未記録");
                    Assert.AreEqual(edge.Index, edge.SocketA.UsedByEdge);
                    Assert.AreEqual(edge.Index, edge.SocketB.UsedByEdge);

                    Assert.AreEqual(edge.SocketA.Facing.Opposite(), edge.SocketB.Facing,
                        $"seed {seed}: 辺 {edge.Index} の 2 つのソケットが向かい合っていない");
                    Assert.AreEqual(edge.SocketB.Cell, edge.SocketA.ExitCell,
                        $"seed {seed}: 辺 {edge.Index} の 2 つのソケットが隣接していない");
                    Assert.AreEqual(edge.SocketA.Cell, edge.SocketB.ExitCell);

                    Assert.IsTrue(layout.Rooms[edge.RoomA].Bounds.Contains(edge.SocketA.Cell),
                        $"seed {seed}: 辺 {edge.Index} の A 側ドアが部屋 {edge.RoomA} の外にある");
                    Assert.IsTrue(layout.Rooms[edge.RoomB].Bounds.Contains(edge.SocketB.Cell),
                        $"seed {seed}: 辺 {edge.Index} の B 側ドアが部屋 {edge.RoomB} の外にある");
                    // 重なっていなくて距離 1 = ちょうど辺を共有している（角だけ触れていれば 2 になる）。
                    Assert.AreEqual(1, GridRect.Distance(layout.Rooms[edge.RoomA].Bounds, layout.Rooms[edge.RoomB].Bounds),
                        $"seed {seed}: 辺 {edge.Index} の 2 部屋が辺を共有していない");
                }
            }
        }

        [Test]
        public void DoorCount_MatchesEdgeCountTimesTwo()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                int doorCount = layout.Rooms.Sum(r => r.UsedSockets().Count());
                Assert.AreEqual(layout.Edges.Count * 2, doorCount, $"seed {seed}: ドア数が辺の本数と合わない");
            }
        }

        [Test]
        public void NoTwoEdges_ConnectTheSameRoomPair()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                var seen = new HashSet<(int, int)>();

                foreach (var edge in layout.Edges)
                {
                    var key = (System.Math.Min(edge.RoomA, edge.RoomB), System.Math.Max(edge.RoomA, edge.RoomB));
                    Assert.IsTrue(seen.Add(key), $"seed {seed}: 部屋 {key} に 2 枚目のドアが開いている");
                }
            }
        }

        /// <summary>
        /// 生成した辺が、具現化する側（FloorOccupancy）からもドアとして見えているか。
        /// ここが食い違うと、レイアウト上は繋がっているのにシーンでは壁で塞がれる。
        /// </summary>
        [Test]
        public void EveryEdge_IsReportedAsADoorByOccupancy()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                var occupancy = new FloorOccupancy(layout);

                foreach (var edge in layout.Edges)
                {
                    Assert.AreEqual(BoundaryKind.Door, occupancy.BoundaryAt(edge.SocketA.Cell, edge.SocketA.Facing),
                        $"seed {seed}: 辺 {edge.Index} が A 側からドアとして見えていない");
                    Assert.AreEqual(BoundaryKind.Door, occupancy.BoundaryAt(edge.SocketB.Cell, edge.SocketB.Facing),
                        $"seed {seed}: 辺 {edge.Index} が B 側からドアとして見えていない");
                }
            }
        }

        /// <summary>
        /// 使われなかったドア候補はただの壁になる。プレハブに打ったソケットの位置以外に
        /// 勝手にドアが開かないこと、開くと決めた所以外は塞がることの確認。
        /// </summary>
        [Test]
        public void UnusedSockets_StayWalls()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                var occupancy = new FloorOccupancy(layout);

                foreach (var room in layout.Rooms)
                {
                    foreach (var socket in room.Sockets)
                    {
                        if (socket.IsUsed) continue;
                        Assert.AreEqual(BoundaryKind.Wall, occupancy.BoundaryAt(socket.Cell, socket.Facing),
                            $"seed {seed}: 使っていないドア候補 {socket} が壁になっていない");
                    }
                }
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
                Assert.GreaterOrEqual(layout.ShopRoom, 0, $"seed {seed}: ショップが無い");

                Assert.AreNotEqual(layout.StairUpRoom, layout.StairDownRoom, $"seed {seed}: 階段が同じ部屋にある");
                Assert.AreNotEqual(layout.StairUpRoom, layout.ShopRoom);
                Assert.AreNotEqual(layout.StairDownRoom, layout.ShopRoom);

                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.StairUp));
                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.StairDown));
                Assert.AreEqual(1, layout.Rooms.Count(r => r.Role == RoomRole.Shop));
            }
        }

        [Test]
        public void IncludeShopFalse_LeavesNoShop()
        {
            var parameters = FloorGenerationFixture.Params();
            parameters.IncludeShop = false;

            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);

                Assert.AreEqual(-1, layout.ShopRoom, $"seed {seed}: ショップを作らない設定なのに割り当てられている");
                Assert.IsEmpty(layout.Rooms.Where(r => r.Role == RoomRole.Shop));
            }
        }
    }
}
