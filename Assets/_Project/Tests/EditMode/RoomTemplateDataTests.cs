using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    public sealed class RoomTemplateDataTests
    {
        [Test]
        public void SizeAfterRotation_SwapsOnOddSteps()
        {
            var template = FloorGenerationFixture.Template("T", 8, 6, RoomTag.Normal, true);

            Assert.AreEqual((8, 6), template.SizeAfterRotation(0));
            Assert.AreEqual((6, 8), template.SizeAfterRotation(1));
            Assert.AreEqual((8, 6), template.SizeAfterRotation(2));
            Assert.AreEqual((6, 8), template.SizeAfterRotation(3));
        }

        /// <summary>
        /// セルの回転と向きの回転の手回りが食い違うと、East 辺のセルが West を向くようなソケットができて
        /// 壁の中にドアが生まれる。回帰防止のため、回転後もソケットが必ず自分の辺の外を向くことを確認する。
        /// </summary>
        [Test]
        public void SocketsAfterRotation_StayOnTheSideTheyFace()
        {
            var template = FloorGenerationFixture.Template("T", 10, 6, RoomTag.Normal, true);

            for (int rotation = 0; rotation < 4; rotation++)
            {
                var (width, height) = template.SizeAfterRotation(rotation);
                foreach (var socket in template.SocketsAfterRotation(rotation))
                {
                    var cell = socket.LocalCell;
                    Assert.That(cell.X, Is.InRange(0, width - 1), $"rot{rotation} のソケット {socket} が範囲外");
                    Assert.That(cell.Y, Is.InRange(0, height - 1), $"rot{rotation} のソケット {socket} が範囲外");

                    var outside = cell + socket.Facing.Offset();
                    bool leavesTheRoom = outside.X < 0 || outside.Y < 0 || outside.X >= width || outside.Y >= height;
                    Assert.IsTrue(leavesTheRoom, $"rot{rotation} のソケット {socket} が部屋の内側を向いている (size {width}x{height})");
                }
            }
        }

        [Test]
        public void RotateCell_MatchesDirectionRotation()
        {
            var template = FloorGenerationFixture.Template("T", 5, 3, RoomTag.Normal, true);

            // North を向いたベクトルは 90 度時計回りで East になる。セルの回転も同じ手回りでなければならない。
            var origin = new GridPos(0, 0);
            var north = new GridPos(0, 1);
            var rotatedOrigin = template.RotateCell(origin, 1);
            var rotatedNorth = template.RotateCell(north, 1);
            var delta = rotatedNorth - rotatedOrigin;

            Assert.AreEqual(Direction.North.RotateCw(1).Offset(), delta);
        }

        [Test]
        public void DefaultSockets_CoverEverySide()
        {
            foreach (var (width, height) in new[] { (6, 6), (8, 6), (10, 8), (12, 10), (8, 8) })
            {
                var template = FloorGenerationFixture.Template($"T{width}x{height}", width, height, RoomTag.Normal, true);
                Assert.IsTrue(template.HasSocketOnEverySide(), $"{width}x{height} の四辺にソケットが揃っていない");
            }
        }
    }
}
