using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>部屋の置き物の置き方：ドアの前・階段を空ける、部屋からはみ出さない、同じ乱数なら同じ、ボス階は松明と柱だけ。</summary>
    public sealed class RoomPropPlannerTests
    {
        private const float Cell = 5f;

        private static RoomPropSettings Settings(float crates = 0.5f, float chest = 0.3f, bool boss = false) => new RoomPropSettings
        {
            CellSize = Cell,
            CratesPerCell = crates,
            ChestChance = chest,
            BossFloor = boss,
        };

        private static Func<GridPos, Direction, RoomEdgeKind> Edges(FloorOccupancy occupancy) =>
            (cell, dir) => occupancy.BoundaryAt(cell, dir) == BoundaryKind.Door ? RoomEdgeKind.Door : RoomEdgeKind.Wall;

        private static List<RoomPropPlacement> Plan(FloorLayout layout, FloorOccupancy occupancy, RoomInstance room, RoomPropSettings settings) =>
            RoomPropPlanner.PlanRoom(room, Edges(occupancy), settings,
                new Random(RoomEnemyRoller.RoomSeed(layout.Seed, room.Index, RoomPropPlanner.PropSalt)));

        private static bool OnFloor(RoomPropKind kind) => kind != RoomPropKind.Torch && kind != RoomPropKind.TableTop && kind != RoomPropKind.Chair;

        /// <summary>ドアの内側の空けておく四角に、点が入っているか。</summary>
        private static bool InDoorway(RoomInstance room, FloorOccupancy occupancy, RoomPropSettings settings, float x, float z)
        {
            var b = room.Bounds;
            for (int cx = b.X; cx <= b.MaxX; cx++)
            {
                for (int cy = b.Y; cy <= b.MaxY; cy++)
                {
                    var cell = new GridPos(cx, cy);
                    foreach (var dir in DirectionUtil.All)
                    {
                        if (!b.Contains(cell) || b.Contains(cell + dir.Offset())) continue;
                        if (occupancy.BoundaryAt(cell, dir) != BoundaryKind.Door) continue;

                        var step = dir.Offset();
                        float doorX = (cx + 0.5f + step.X * 0.5f) * Cell, doorZ = (cy + 0.5f + step.Y * 0.5f) * Cell;
                        float depthX = step.X != 0 ? settings.DoorClearDepth : settings.DoorClearWidth;
                        float depthZ = step.Y != 0 ? settings.DoorClearDepth : settings.DoorClearWidth;
                        // 内側へ DoorClearDepth、壁に沿って DoorClearWidth の四角。
                        float centerX = doorX - step.X * settings.DoorClearDepth * 0.5f, centerZ = doorZ - step.Y * settings.DoorClearDepth * 0.5f;
                        if (Math.Abs(x - centerX) < depthX * 0.5f && Math.Abs(z - centerZ) < depthZ * 0.5f) return true;
                    }
                }
            }
            return false;
        }

        [Test]
        public void ランダムな階で_部屋からはみ出さず_ドアの前と階段のセルには置かない()
        {
            var settings = Settings();
            int floorProps = 0, crates = 0, chests = 0, torches = 0;
            foreach (int seed in FloorGenerationFixture.Seeds(60))
            {
                var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), seed);
                var occupancy = new FloorOccupancy(layout);
                foreach (var room in layout.Rooms)
                {
                    var b = room.Bounds;
                    foreach (var p in Plan(layout, occupancy, room, settings))
                    {
                        Assert.That(p.X, Is.InRange(b.X * Cell, (b.MaxX + 1) * Cell), $"シード {seed} の {room} の {p}");
                        Assert.That(p.Z, Is.InRange(b.Y * Cell, (b.MaxY + 1) * Cell), $"シード {seed} の {room} の {p}");
                        if (p.Kind == RoomPropKind.Torch) torches++;
                        if (!OnFloor(p.Kind)) continue;

                        floorProps++;
                        Assert.IsFalse(InDoorway(room, occupancy, settings, p.X, p.Z), $"シード {seed} の {room} の {p} がドアの前にある");
                        if (room.Role != RoomRole.Normal)
                        {
                            var f = room.FeatureCell;
                            bool inFeature = p.X > f.X * Cell && p.X < (f.X + 1) * Cell && p.Z > f.Y * Cell && p.Z < (f.Y + 1) * Cell;
                            Assert.IsFalse(inFeature, $"シード {seed} の {room}（{room.Role}）の {p} が階段・目印のセルにある");
                            Assert.AreNotEqual(RoomPropKind.Crate, p.Kind, "箱は普通の部屋だけ");
                            Assert.AreNotEqual(RoomPropKind.Chest, p.Kind, "宝箱は普通の部屋だけ");
                        }
                        if (p.Kind == RoomPropKind.Crate) crates++;
                        if (p.Kind == RoomPropKind.Chest) chests++;
                    }
                }
            }

            Assert.Greater(floorProps, 0);
            Assert.Greater(crates, 0, "木箱が 1 つも置かれていない");
            Assert.Greater(chests, 0, "宝箱が 1 つも置かれていない");
            Assert.Greater(torches, 0, "松明が 1 つも付いていない");
        }

        [Test]
        public void 床の置き物どうしは重ならない()
        {
            var settings = Settings(crates: 1.5f, chest: 1f);
            foreach (int seed in FloorGenerationFixture.Seeds(30))
            {
                var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), seed);
                var occupancy = new FloorOccupancy(layout);
                foreach (var room in layout.Rooms)
                {
                    // 木箱・宝箱は床の上の大きさが分かっているので、四角どうしで確かめる。
                    var boxes = Plan(layout, occupancy, room, settings).Where(p => p.Kind == RoomPropKind.Crate || p.Kind == RoomPropKind.Chest).ToList();
                    for (int i = 0; i < boxes.Count; i++)
                    {
                        for (int j = i + 1; j < boxes.Count; j++)
                        {
                            var (ax, az) = Footprint(boxes[i], settings);
                            var (bx, bz) = Footprint(boxes[j], settings);
                            float dx = Math.Abs(boxes[i].X - boxes[j].X), dz = Math.Abs(boxes[i].Z - boxes[j].Z);
                            Assert.IsTrue(dx >= (ax + bx) * 0.5f - 0.01f || dz >= (az + bz) * 0.5f - 0.01f,
                                $"シード {seed} の {room} の {boxes[i]} と {boxes[j]} が重なっている");
                        }
                    }
                }
            }
        }

        /// <summary>木箱は斜めに置くので見た目の対角まで、宝箱は向きで縦横が入れ替わる。</summary>
        private static (float x, float z) Footprint(RoomPropPlacement p, RoomPropSettings settings)
        {
            if (p.Kind == RoomPropKind.Crate) return (settings.CrateSize, settings.CrateSize);
            bool facesNorthSouth = Math.Abs(Math.Sin(p.Yaw * Math.PI / 180d)) < 0.5d;
            return facesNorthSouth ? (settings.ChestWidth, settings.ChestDepth) : (settings.ChestDepth, settings.ChestWidth);
        }

        [Test]
        public void 同じ部屋と同じ乱数なら同じ置き方になる()
        {
            var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), 7);
            var occupancy = new FloorOccupancy(layout);
            foreach (var room in layout.Rooms)
            {
                var a = Plan(layout, occupancy, room, Settings());
                var b = Plan(layout, occupancy, room, Settings());
                CollectionAssert.AreEqual(a.Select(p => p.ToString() + p.Variant), b.Select(p => p.ToString() + p.Variant));
            }
        }

        [Test]
        public void 量を0にすると木箱と宝箱を置かない()
        {
            var settings = Settings(crates: 0f, chest: 0f);
            foreach (int seed in FloorGenerationFixture.Seeds(20))
            {
                var layout = FloorLayoutGenerator.Generate(FloorGenerationFixture.Params(), seed);
                var occupancy = new FloorOccupancy(layout);
                foreach (var room in layout.Rooms)
                {
                    Assert.IsFalse(Plan(layout, occupancy, room, settings).Any(p => p.Kind == RoomPropKind.Crate || p.Kind == RoomPropKind.Chest));
                }
            }
        }

        [Test]
        public void 机の確率が0なら机を置かず_1なら広い部屋に置く()
        {
            var template = FloorGenerationFixture.Template("Room_3x3", 3, 3, RoomTag.Normal, true);
            var room = new RoomInstance(0, template, 0, new GridRect(0, 0, 3, 3), new List<PlacedSocket>());
            var never = Settings();
            never.TableChance = 0f;
            var always = Settings();
            always.TableChance = 1f;
            int placed = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                Assert.IsFalse(RoomPropPlanner.PlanRoom(room, (c, d) => RoomEdgeKind.Wall, never, new Random(seed)).Any(p => p.Kind == RoomPropKind.Table));
                if (RoomPropPlanner.PlanRoom(room, (c, d) => RoomEdgeKind.Wall, always, new Random(seed)).Any(p => p.Kind == RoomPropKind.Table)) placed++;
            }
            Assert.Greater(placed, 15, "確率 1 なら（柱と重ならない限り）ほとんどの部屋に置く");
        }

        [Test]
        public void 松明は窓とドアの辺には付けない()
        {
            var template = FloorGenerationFixture.Template("Room_3x3", 3, 3, RoomTag.Normal, true);
            var room = new RoomInstance(0, template, 0, new GridRect(10, 10, 3, 3), new List<PlacedSocket>());
            // 南の辺は全部ドア、北の辺は全部窓。
            RoomEdgeKind EdgeAt(GridPos cell, Direction dir) =>
                dir == Direction.South ? RoomEdgeKind.Door : dir == Direction.North ? RoomEdgeKind.Window : RoomEdgeKind.Wall;

            var torches = RoomPropPlanner.PlanRoom(room, EdgeAt, Settings(), new Random(1)).Where(p => p.Kind == RoomPropKind.Torch).ToList();
            Assert.IsNotEmpty(torches);
            foreach (var t in torches)
            {
                bool westOrEast = Math.Abs(t.X - 10 * Cell) < 0.2f || Math.Abs(t.X - 13 * Cell) < 0.2f;
                Assert.IsTrue(westOrEast, $"{t} は東西の壁に付くはず");
                // 内側を向く（西の壁なら東 = 90 度、東の壁なら西 = 270 度）。
                Assert.AreEqual(t.X < 11 * Cell ? 90f : 270f, t.Yaw, 0.01f);
            }
        }

        [Test]
        public void 広い部屋には柱を立てることがある()
        {
            var template = FloorGenerationFixture.Template("Room_3x3", 3, 3, RoomTag.Normal, true);
            var room = new RoomInstance(0, template, 0, new GridRect(0, 0, 3, 3), new List<PlacedSocket>());
            int withPillars = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var plan = RoomPropPlanner.PlanRoom(room, (c, d) => RoomEdgeKind.Wall, Settings(), new Random(seed));
                var pillars = plan.Where(p => p.Kind == RoomPropKind.Pillar).ToList();
                if (pillars.Count == 0) continue;
                withPillars++;
                foreach (var p in pillars)
                {
                    // 内側の格子の角（5 m・10 m）に立つ。
                    Assert.That(p.X, Is.EqualTo(5f).Or.EqualTo(10f));
                    Assert.That(p.Z, Is.EqualTo(5f).Or.EqualTo(10f));
                }
            }
            Assert.That(withPillars, Is.InRange(5, 35), "半分くらいの部屋に柱が立つ");
        }

        [Test]
        public void ボス階は松明と柱だけで_闘技場の真ん中を空ける()
        {
            var layout = BossFloorLayout.Generate(FloorGenerationFixture.BossTemplates(), 16, 16, 3);
            var occupancy = new FloorOccupancy(layout);
            var settings = Settings(crates: 2f, chest: 1f, boss: true);
            int pillars = 0;
            foreach (var room in layout.Rooms)
            {
                foreach (var p in Plan(layout, occupancy, room, settings))
                {
                    Assert.That(p.Kind, Is.EqualTo(RoomPropKind.Torch).Or.EqualTo(RoomPropKind.Pillar), $"{room} の {p}");
                    if (p.Kind != RoomPropKind.Pillar) continue;
                    pillars++;
                    Assert.AreEqual("Room_BossArena", room.Template.Id);
                    var c = room.Bounds.Center;
                    float cx = (c.X + 0.5f) * Cell, cz = (c.Y + 0.5f) * Cell;
                    Assert.IsTrue(Math.Abs(p.X - cx) > 1.5f * Cell || Math.Abs(p.Z - cz) > 1.5f * Cell, $"{p} が闘技場の真ん中にある");
                }
            }
            Assert.AreEqual(4, pillars, "闘技場の真ん中の四隅に 1 本ずつ");
        }

        [Test]
        public void ボス階の道のりの部屋は木箱と宝箱を置かず_ショートカットの部屋は井戸の所を空ける()
        {
            var parameters = new BossFloorParams { Width = 40, Height = 40, Templates = FloorGenerationFixture.BossTemplates(), LayoutSeed = 5, PathRooms = 4, SideRooms = 2 };
            var layout = BossFloorLayout.Generate(parameters, 3);
            var occupancy = new FloorOccupancy(layout);
            var settings = Settings(crates: 3f, chest: 1f, boss: true);
            int pathProps = 0;
            foreach (var room in layout.Rooms)
            {
                bool bossRoom = (room.Template.Tags & RoomTag.Boss) != 0;
                foreach (var p in Plan(layout, occupancy, room, settings))
                {
                    Assert.AreNotEqual(RoomPropKind.Crate, p.Kind, $"{room} に木箱");
                    Assert.AreNotEqual(RoomPropKind.Chest, p.Kind, $"{room} に宝箱");
                    if (bossRoom) Assert.That(p.Kind, Is.EqualTo(RoomPropKind.Torch).Or.EqualTo(RoomPropKind.Pillar), $"{room} の {p}");
                    else if (room.Role == RoomRole.Normal && p.Kind != RoomPropKind.Torch) pathProps++;

                    if (room.Template.Id != "Room_BossShortcut" || !OnFloor(p.Kind)) continue;
                    var c = room.Bounds.Center;
                    Assert.IsTrue(Math.Abs(p.X - (c.X + 0.5f) * Cell) > Cell * 0.5f || Math.Abs(p.Z - (c.Y + 0.5f) * Cell) > Cell * 0.5f,
                        $"{p} がショートカットの部屋の真ん中（井戸の所）にある");
                }
            }
            Assert.Greater(pathProps, 0, "道のりの部屋には机や小物を置く");
        }
    }
}
