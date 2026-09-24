using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>マップに「入った部屋だけ」を出すための探索記録。</summary>
    public sealed class FloorExplorationTests
    {
        private const int SeedSweepCount = 20;

        [Test]
        public void EveryRoomCell_ResolvesToThatRoom()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var layout = FloorLayoutGenerator.Generate(parameters, seed);
                var exploration = new FloorExploration(layout);

                foreach (var room in layout.Rooms)
                {
                    for (int y = room.Bounds.Y; y <= room.Bounds.MaxY; y++)
                    {
                        for (int x = room.Bounds.X; x <= room.Bounds.MaxX; x++)
                        {
                            Assert.AreEqual(room.Index, exploration.RoomAt(new GridPos(x, y)),
                                $"seed {seed}: ({x},{y}) が部屋 {room.Index} にならない");
                        }
                    }
                }
            }
        }

        [Test]
        public void NothingIsVisited_BeforeEnteringAnyRoom()
        {
            var exploration = new FloorExploration(FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 1));

            Assert.AreEqual(0, exploration.VisitedCount);
            Assert.AreEqual(-1, exploration.CurrentRoom);
            Assert.IsFalse(exploration.IsVisited(0));
        }

        [Test]
        public void Visit_MarksRoomOnce_AndTracksCurrentRoom()
        {
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 1);
            var exploration = new FloorExploration(layout);
            var first = layout.Rooms[0];
            var second = layout.Rooms[1];

            Assert.IsTrue(exploration.Visit(first.Bounds.Center), "初めて入った部屋は新しく記録される");
            Assert.IsFalse(exploration.Visit(first.Bounds.Center), "同じ部屋に居続けても記録は増えない");
            Assert.IsTrue(exploration.Visit(second.Bounds.Center));
            Assert.IsFalse(exploration.Visit(first.Bounds.Center), "戻っただけでは新しくならない");

            Assert.AreEqual(2, exploration.VisitedCount);
            Assert.AreEqual(first.Index, exploration.CurrentRoom);
            Assert.IsTrue(exploration.IsVisited(first.Index));
            Assert.IsTrue(exploration.IsVisited(second.Index));
        }

        [Test]
        public void Visit_OutsideRooms_KeepsPreviousRoom()
        {
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 1);
            var exploration = new FloorExploration(layout);
            var room = layout.Rooms[0];

            exploration.Visit(room.Bounds.Center);

            Assert.IsFalse(exploration.Visit(new GridPos(-5, -5)));
            Assert.AreEqual(room.Index, exploration.CurrentRoom, "壁の外や境界の上では直前の部屋のまま");
            Assert.AreEqual(-1, exploration.RoomAt(new GridPos(-5, -5)));
        }
    }
}
