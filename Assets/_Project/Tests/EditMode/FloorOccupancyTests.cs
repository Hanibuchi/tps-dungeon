using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    public sealed class FloorOccupancyTests
    {
        private const int SeedSweepCount = 50;

        /// <summary>
        /// 床のあるセルが外に向かって開いていないこと。ここが抜けると、生成されたフロアに
        /// 壁の無い穴ができて外に出られてしまう。
        /// </summary>
        [Test]
        public void EveryFloorCell_IsSealedAgainstEmptySpace()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var occupancy = new FloorOccupancy(FloorLayoutGenerator.Generate(parameters, seed));

                for (int y = 0; y < occupancy.Height; y++)
                {
                    for (int x = 0; x < occupancy.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        if (occupancy.KindAt(cell) == CellKind.Empty) continue;

                        foreach (var dir in DirectionUtil.All)
                        {
                            if (occupancy.KindAt(cell + dir.Offset()) != CellKind.Empty) continue;
                            Assert.AreEqual(BoundaryKind.Wall, occupancy.BoundaryAt(cell, dir),
                                $"seed {seed}: {cell} の {dir} 側が塞がれていない");
                        }
                    }
                }
            }
        }

        /// <summary>同じ境界を両側から問い合わせたら同じ答えが返ること。壁とドアが片面だけ建つのを防ぐ。</summary>
        [Test]
        public void Boundaries_AgreeFromBothSides()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(SeedSweepCount))
            {
                var occupancy = new FloorOccupancy(FloorLayoutGenerator.Generate(parameters, seed));

                for (int y = 0; y < occupancy.Height; y++)
                {
                    for (int x = 0; x < occupancy.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        if (occupancy.KindAt(cell) == CellKind.Empty) continue;

                        foreach (var dir in DirectionUtil.All)
                        {
                            var neighbor = cell + dir.Offset();
                            if (occupancy.KindAt(neighbor) == CellKind.Empty) continue;

                            Assert.AreEqual(occupancy.BoundaryAt(cell, dir), occupancy.BoundaryAt(neighbor, dir.Opposite()),
                                $"seed {seed}: 境界 {cell}/{neighbor} の判定が両側で食い違う");
                        }
                    }
                }
            }
        }
    }
}
