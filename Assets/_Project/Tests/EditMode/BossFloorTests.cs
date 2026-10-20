using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// ボス階の間取り（入口 → 道のり → 横長の部屋 → 大扉 → 5x5 のボス部屋、横長の部屋の東にショートカットの部屋）と、
    /// それを今の設定（Floor_L*_Boss_Config）で組んだときに大扉・閉ざされた扉・井戸・窓が揃うこと、大扉が両側へ開くことを確かめる。
    /// </summary>
    public sealed class BossFloorTests
    {
        private const int GridSize = 16;

        private static BossFloorTemplates Templates() => FloorGenerationFixture.BossTemplates();

        private static BossFloorParams PathParams(int layoutSeed, int pathRooms = 4, int sideRooms = 2) => new BossFloorParams
        {
            Width = 40,
            Height = 40,
            Templates = Templates(),
            LayoutSeed = layoutSeed,
            PathRooms = pathRooms,
            SideRooms = sideRooms,
        };

        private static RoomInstance RoomWithId(FloorLayout layout, string id)
        {
            foreach (var room in layout.Rooms)
            {
                if (room.Template.Id == id) return room;
            }
            return null;
        }

        /// <summary>シグネチャのうち、形の部分（最初の | より後。先頭のグリッドの広さは除く）。</summary>
        private static string Shape(FloorLayout layout)
        {
            string signature = layout.ComputeSignature();
            return signature.Substring(signature.IndexOf('|'));
        }

        [Test]
        public void Layout_StacksEntranceHallArenaAndExitAlongTheCenterColumn()
        {
            var layout = BossFloorLayout.Generate(Templates(), GridSize, GridSize, 1);
            Assert.AreEqual(5, layout.Rooms.Count);
            int center = layout.MirrorColumn.Value;

            var entrance = layout.RoomAt(layout.EntranceRoom);
            var exit = layout.RoomAt(layout.ExitRoom);
            var hall = layout.Rooms[1];
            var arena = layout.Rooms[2];
            Assert.AreEqual("Room_Stair", entrance.Template.Id);
            Assert.AreEqual(new GridRect(center, entrance.Bounds.Y, 1, 1), entrance.Bounds);
            Assert.AreEqual(new GridRect(center - 1, entrance.Bounds.Y + 1, 3, 1), hall.Bounds, "入口のすぐ奥（北）に横長の部屋");
            Assert.AreEqual(new GridRect(center - 2, entrance.Bounds.Y + 2, 5, 5), arena.Bounds, "その奥に 5x5 のボス部屋");
            Assert.AreEqual(-1, layout.ShopRoom);

            // 間取りはグリッドの真ん中に置く（外の景色はグリッドの真ん中を基準に置く）。道のりの無い層は今までと同じ所。
            Assert.AreEqual(7, center);
            Assert.AreEqual(4, entrance.Bounds.Y);

            // 出口はボス部屋の奥の壁の真ん中に接する 1x1 の部屋で、階段の部屋をそのまま（ドアが南）置く。
            Assert.AreEqual("Room_Stair", exit.Template.Id);
            Assert.AreEqual(new GridRect(center, arena.Bounds.MaxY + 1, 1, 1), exit.Bounds);
            Assert.AreEqual(0, exit.Rotation);
            Assert.AreEqual(RoomRole.Exit, exit.Role);
            Assert.AreEqual(RoomRole.Normal, arena.Role);
            Assert.AreEqual(RoomRole.Entrance, entrance.Role);

            // ショートカットの部屋は横長の部屋の東隣の 1x1。井戸はその真ん中。
            var shortcut = RoomWithId(layout, "Room_BossShortcut");
            Assert.AreEqual(new GridRect(hall.Bounds.MaxX + 1, hall.Bounds.Y, 1, 1), shortcut.Bounds);
            Assert.AreEqual(shortcut.Bounds.Center, layout.ShortcutCell);
            Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout));
        }

        [Test]
        public void Layout_IsSymmetricWithAGrandDoorInALockedDoorOutAndALockedShortcutDoor()
        {
            var layout = BossFloorLayout.Generate(Templates(), GridSize, GridSize, 1);
            int axis = layout.MirrorColumn.Value;

            foreach (var room in layout.Rooms)
            {
                if (room.Template.Id == "Room_BossShortcut") continue;
                Assert.AreEqual(2 * axis, room.Bounds.X + room.Bounds.MaxX, $"{room} が軸 {axis} で左右対称でない");
            }

            Assert.AreEqual(4, layout.Edges.Count);
            int grand = 0, locked = 0, shortcut = 0;
            foreach (var edge in layout.Edges)
            {
                Assert.LessOrEqual((edge.IsGrandDoor ? 1 : 0) + (edge.IsLocked ? 1 : 0) + (edge.IsShortcutDoor ? 1 : 0), 1);
                if (edge.IsShortcutDoor)
                {
                    shortcut++;
                    Assert.AreEqual(1, edge.RoomA, "ショートカットの扉の手前は横長の部屋");
                    Assert.AreEqual("Room_BossShortcut", layout.RoomAt(edge.RoomB).Template.Id);
                    Assert.AreEqual(Direction.East, edge.SocketA.Facing, "横長の部屋の東端から入る");
                    continue;
                }
                Assert.AreEqual(axis, edge.SocketA.Cell.X, $"{edge} のドアが真ん中の列に無い");
                Assert.AreEqual(Direction.North, edge.SocketA.Facing, "ドアはどれも奥（北）へ抜ける");
                if (edge.IsGrandDoor)
                {
                    grand++;
                    Assert.AreEqual(1, edge.RoomA, "大扉の手前は横長の部屋");
                    Assert.AreEqual(2, edge.RoomB, "大扉の先はボス部屋");
                }
                if (edge.IsLocked)
                {
                    locked++;
                    Assert.AreEqual(2, edge.RoomA, "閉ざされた扉の手前はボス部屋");
                    Assert.AreEqual(layout.ExitRoom, edge.RoomB, "閉ざされた扉の先は出口");
                }
            }
            Assert.AreEqual(1, grand, "大扉は 1 枚だけ");
            Assert.AreEqual(1, locked, "閉ざされた扉は 1 枚だけ");
            Assert.AreEqual(1, shortcut, "ショートカットの扉は 1 枚だけ");

            // 出口の部屋のドアは南（ボス部屋の側）だけ。
            foreach (var socket in layout.RoomAt(layout.ExitRoom).UsedSockets()) Assert.AreEqual(Direction.South, socket.Facing);

            // 入口の部屋はドアが北に向くよう半周回してある（階段は部屋と一緒に回る）。
            var entrance = layout.RoomAt(layout.EntranceRoom);
            Assert.AreEqual(2, entrance.Rotation);
            foreach (var socket in entrance.UsedSockets()) Assert.AreEqual(Direction.North, socket.Facing);
        }

        [Test]
        public void Layout_ShapeDoesNotDependOnSeed()
        {
            Assert.AreEqual(Shape(BossFloorLayout.Generate(Templates(), GridSize, GridSize, 1)), Shape(BossFloorLayout.Generate(Templates(), GridSize, GridSize, 999)));
            // 道のりのある層も、形は LayoutSeed だけで決まる（来るたびに変わるシードは窓などにだけ効く）。
            Assert.AreEqual(Shape(BossFloorLayout.Generate(PathParams(7), 1)), Shape(BossFloorLayout.Generate(PathParams(7), 12345)));
        }

        [Test]
        public void Layout_RejectsWrongSizedTemplates()
        {
            var templates = Templates();
            templates.Arena = FloorGenerationFixture.Template("Room_BossArena", 4, 4, RoomTag.Boss, false, 0f);
            Assert.Throws<System.ArgumentException>(() => BossFloorLayout.Generate(templates, GridSize, GridSize, 1));

            templates = Templates();
            templates.Shortcut = null;
            Assert.Throws<System.ArgumentException>(() => BossFloorLayout.Generate(templates, GridSize, GridSize, 1), "ショートカットの部屋は必ず要る");
        }

        [Test]
        public void PathLayout_GrowsTheRequestedRoomsFromTheHallAndKeepsTheShortcutRoomApart()
        {
            var shapes = new HashSet<string>();
            for (int seed = 1; seed <= 60; seed++)
            {
                var parameters = PathParams(seed);
                var layout = BossFloorLayout.Generate(parameters, 1);
                string label = $"シード {seed}";
                Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout), $"{label}: 入口から行けない部屋がある");
                Assert.IsNull(layout.MirrorColumn, $"{label}: 道のりのある層は左右対称でない");
                shapes.Add(Shape(layout));

                int normal = 0;
                foreach (var room in layout.Rooms)
                {
                    Assert.IsTrue(layout.IsInsideGrid(new GridPos(room.Bounds.X, room.Bounds.Y)) && layout.IsInsideGrid(new GridPos(room.Bounds.MaxX, room.Bounds.MaxY)), $"{label}: {room} がグリッドの外");
                    if ((room.Template.Tags & RoomTag.Normal) != 0) normal++;
                    foreach (var other in layout.Rooms)
                    {
                        if (other != room) Assert.IsFalse(room.Bounds.Overlaps(other.Bounds), $"{label}: {room} と {other} が重なる");
                    }
                }
                Assert.AreEqual(parameters.PathRooms + parameters.SideRooms, normal, $"{label}: 道のりと脇道の部屋の数");

                var hall = RoomWithId(layout, "Room_BossHall");
                var arena = RoomWithId(layout, "Room_BossArena");
                var shortcut = RoomWithId(layout, "Room_BossShortcut");
                var entrance = layout.RoomAt(layout.EntranceRoom);
                Assert.AreEqual(5, arena.Bounds.Width);
                Assert.AreEqual(5, arena.Bounds.Height);
                Assert.AreEqual(new GridRect(hall.Bounds.MaxX + 1, hall.Bounds.Y, 1, 1), shortcut.Bounds, $"{label}: ショートカットの部屋は横長の部屋の東隣");
                Assert.Greater(GridRect.Distance(entrance.Bounds, hall.Bounds), 1, $"{label}: 入口が横長の部屋に接している");
                Assert.Greater(GridRect.Distance(entrance.Bounds, arena.Bounds), 1, $"{label}: 入口がボス部屋に接している");

                foreach (var edge in layout.Edges)
                {
                    var a = layout.RoomAt(edge.RoomA);
                    var b = layout.RoomAt(edge.RoomB);
                    // ショートカットの部屋は横長の部屋とだけ繋がり、道のりの部屋はショートカットの部屋に接しない。
                    if (a == shortcut || b == shortcut) Assert.IsTrue(edge.IsShortcutDoor && (a == hall || b == hall), $"{label}: {edge}");
                    // ボス部屋に入るのは大扉だけ。
                    if ((a == arena || b == arena) && a != layout.RoomAt(layout.ExitRoom) && b != layout.RoomAt(layout.ExitRoom))
                        Assert.IsTrue(edge.IsGrandDoor, $"{label}: ボス部屋に大扉以外のドア {edge}");
                }
                foreach (var room in layout.Rooms)
                {
                    if ((room.Template.Tags & RoomTag.Normal) != 0)
                        Assert.Greater(GridRect.Distance(room.Bounds, shortcut.Bounds), 1, $"{label}: {room} がショートカットの部屋に接している");
                }
            }
            Assert.Greater(shapes.Count, 50, "シードごとに形が違う");
        }

        [Test]
        public void ShippedBossFloors_HaveTheirOwnShapeOnEveryLayer()
        {
            var shapes = new HashSet<string>();
            for (int layer = 1; layer <= 5; layer++)
            {
                var config = AssetDatabase.LoadAssetAtPath<FloorConfig>($"Assets/_Project/Settings/Map/Floor_L{layer}_Boss_Config.asset");
                Assert.IsNotNull(config);
                var parameters = config.BuildBossParams();
                Assert.IsNotNull(parameters.Templates.Shortcut, $"L{layer} のカタログに {FloorConfig.BossShortcutId} が無い");
                var layout = BossFloorLayout.Generate(parameters, 1);
                Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout));
                Assert.IsTrue(shapes.Add(Shape(layout)), $"L{layer} の形がほかの層と同じ");

                int normal = 0;
                foreach (var room in layout.Rooms)
                {
                    if ((room.Template.Tags & RoomTag.Normal) != 0) normal++;
                }
                Assert.AreEqual(config.bossPathRooms + config.bossSideRooms, normal, $"L{layer} の道のりの部屋の数");
                if (layer == 1) Assert.AreEqual(0, normal, "1 層は道のりが無い（今までの形）");
                else Assert.Greater(config.bossPathRooms, 0, $"L{layer} には道のりがある");
            }
        }

        [Test]
        public void BossRooms_DoNotHostEnemies()
        {
            var layout = BossFloorLayout.Generate(PathParams(3), 1);
            foreach (var room in layout.Rooms)
            {
                bool normal = (room.Template.Tags & RoomTag.Normal) != 0 && room.Role == RoomRole.Normal;
                Assert.AreEqual(normal, RoomEnemyRoller.CanHostEnemies(room), $"{room}");
            }
        }

        [TestCase(7, 3, Direction.East)]
        [TestCase(5, 9, Direction.West)]
        [TestCase(6, 2, Direction.North)]
        [TestCase(8, 2, Direction.South)]
        public void MirrorCanonical_MatchesForABoundaryAndItsMirror(int x, int y, Direction dir)
        {
            const int axis = 7;
            var mirroredDir = dir == Direction.East ? Direction.West : dir == Direction.West ? Direction.East : dir;
            var a = FloorBuilder.MirrorCanonical(new GridPos(x, y), dir, axis);
            var b = FloorBuilder.MirrorCanonical(new GridPos(2 * axis - x, y), mirroredDir, axis);
            Assert.AreEqual(a, b);
        }

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(1, 1)]
        [TestCase(3, 42)]
        [TestCase(5, 777)]
        public void BossFloor_BuildsGrandDoorLockedShortcutRoomAndWindows(int layer, int seed)
        {
            string path = $"Assets/_Project/Settings/Map/Floor_L{layer}_Boss_Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(path);
            Assert.IsNotNull(config, $"{path} が無い");
            Assert.AreEqual(FloorLayoutShape.Boss, config.layoutShape);

            host = new GameObject("BossFloorTests");
            var builder = host.AddComponent<FloorBuilder>();
            var layout = builder.Build(config, seed);

            var walls = builder.GeneratedRoot.Find("Walls");
            int grand = 0, doors = 0;
            var windows = new HashSet<(int, int, Direction)>();
            foreach (Transform piece in walls)
            {
                if (piece.name.StartsWith("GrandDoor_")) grand++;
                else if (piece.name.StartsWith("Door_")) doors++;
                else if (piece.name.StartsWith("Window_")) windows.Add(Parse(piece.name));
            }
            Assert.AreEqual(1, grand, "大扉は 1 枚");
            Assert.AreEqual(layout.Edges.Count - 1, doors, "大扉のほかは普通のドア（道のり・出口・ショートカットの部屋）");

            // 出口の扉はボスを倒すまで閉ざされていて、調べても開かない。UnlockDoors で開けられるようになる。
            Assert.AreEqual(1, builder.LockedDoors.Count);
            var gate = builder.LockedDoors[0];
            Assert.IsTrue(gate.IsLocked);
            Assert.AreEqual("閉ざされている", gate.PromptLabel);
            var exitRoom = layout.RoomAt(layout.ExitRoom);
            Assert.AreEqual(new GridPos(exitRoom.Bounds.X, exitRoom.Bounds.Y - 1), builder.WorldToCell(gate.transform.position + Vector3.back * 0.1f),
                "閉ざされた扉はボス部屋と出口の部屋の間");
            gate.Interact(null);
            Assert.IsFalse(gate.IsOpen, "閉ざされている間は開かない");
            // ショートカットの部屋の扉も閉ざされているが、ボスを倒しても開かない（解放は層ごとの永続アップグレード）。
            Assert.IsNotNull(builder.ShortcutDoor);
            Assert.IsTrue(builder.ShortcutDoor.IsLocked);
            CollectionAssert.DoesNotContain(builder.LockedDoors, builder.ShortcutDoor);
            builder.UnlockDoors();
            Assert.IsFalse(gate.IsLocked);
            Assert.IsFalse(builder.HasLockedDoors, "ショートカットの扉は出口の階段を閉じる理由に数えない");
            Assert.IsTrue(builder.ShortcutDoor.IsLocked, "ボスを倒してもショートカットの扉は開かない");
            Assert.IsNotNull(builder.GeneratedRoot.GetComponentInChildren<Door>(), "ドアがある");

            // 道のりの無い層は、窓も左右で揃う（境界を軸で映した先も窓）。片側にしか無いショートカットの部屋と、その鏡像の所は除く。
            if (layout.MirrorColumn.HasValue)
            {
                int axis = layout.MirrorColumn.Value;
                var shortcutCell = layout.ShortcutCell.Value;
                var hallWest = new GridPos(2 * axis - shortcutCell.X, shortcutCell.Y);
                foreach (var (x, y, dir) in windows)
                {
                    var c = new GridPos(x, y);
                    var n = c + dir.Offset();
                    if (c == shortcutCell || n == shortcutCell || c == hallWest || n == hallWest) continue;
                    var (mx, my, mdir) = Mirror(x, y, dir, axis);
                    Assert.IsTrue(windows.Contains((mx, my, mdir)) || windows.Contains(Flip(mx, my, mdir)),
                        $"窓 {x},{y},{dir} の鏡像 {mx},{my},{mdir} が窓でない");
                }
            }

            Assert.IsNotNull(builder.ShortcutMarker, "ショートカットの井戸がある");
            var cell = builder.WorldToCell(builder.ShortcutMarker.transform.position);
            Assert.AreEqual(layout.ShortcutCell.Value, cell);
            Assert.IsNotNull(builder.ShortcutWell, "井戸に調べる所（ShortcutPoint）がある");
            Assert.IsFalse(builder.ShortcutWell.IsOpen, "解放していない層の井戸は閉じている");
            Assert.IsFalse(builder.ShortcutWell.CanInteract(null), "閉じている井戸は扉の隙間から照準が届いても調べられない");
            Assert.IsNotNull(builder.GeneratedRoot.Find("Features/ShortcutSpawn"), "地上から登ってきたパーティーが立つ所がある");
            Assert.AreEqual(layout.ShortcutCell.Value, builder.WorldToCell(builder.GeneratedRoot.Find("Features/ShortcutSpawn").position), "ショートカットの部屋に立つ");
            Assert.IsNotNull(builder.EntranceStair);
            Assert.IsNotNull(builder.ExitStair);
            Assert.AreEqual(layout.RoomAt(layout.ExitRoom).FeatureCell, builder.WorldToCell(builder.ExitStair.transform.position));
        }

        private static (int, int, Direction) Parse(string name)
        {
            var parts = name.Split('_');
            return (int.Parse(parts[1]), int.Parse(parts[2]), (Direction)System.Enum.Parse(typeof(Direction), parts[3]));
        }

        private static (int, int, Direction) Mirror(int x, int y, Direction dir, int axis)
        {
            var d = dir == Direction.East ? Direction.West : dir == Direction.West ? Direction.East : dir;
            return (2 * axis - x, y, d);
        }

        /// <summary>同じ境界の、隣のセルから見た表し方（東の境界 = 東隣の西の境界）。</summary>
        private static (int, int, Direction) Flip(int x, int y, Direction dir)
        {
            var o = dir.Offset();
            return (x + o.X, y + o.Y, dir.Opposite());
        }

        [TestCase(1)]
        [TestCase(5)]
        public void GrandDoor_OpensBothLeavesToTheSameSide(int layer)
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>($"Assets/_Project/Settings/Map/Floor_L{layer}_Boss_Config.asset");
            Assert.IsNotNull(config.grandDoorPrefab, "大扉が差されていない");

            var root = Object.Instantiate(config.grandDoorPrefab);
            try
            {
                var door = root.GetComponent<Door>();
                var hinges = new List<Transform>(door.Hinges);
                Assert.AreEqual(2, hinges.Count, "両開き");

                var left = hinges[0].Find("Leaf").GetComponent<BoxCollider>();
                var right = hinges[1].Find("LeafRight").GetComponent<BoxCollider>();
                Vector3 leftCenter = root.transform.InverseTransformPoint(left.transform.TransformPoint(left.center));
                Vector3 rightCenter = root.transform.InverseTransformPoint(right.transform.TransformPoint(right.center));
                Assert.Less(leftCenter.x, 0f, "左の扉板は開口の左半分");
                Assert.Greater(rightCenter.x, 0f, "右の扉板は開口の右半分");
                Assert.AreEqual(-leftCenter.x, rightCenter.x, 0.001f, "左右対称");

                // クリップと同じ向き（左 +Y、右 -Y）に回すと、どちらも片開きのドアと同じ -Z の側へ開く。
                hinges[0].localRotation = Quaternion.Euler(0f, 100f, 0f);
                hinges[1].localRotation = Quaternion.Euler(0f, -100f, 0f);
                leftCenter = root.transform.InverseTransformPoint(left.transform.TransformPoint(left.center));
                rightCenter = root.transform.InverseTransformPoint(right.transform.TransformPoint(right.center));
                Assert.Less(leftCenter.z, -0.3f);
                Assert.Less(rightCenter.z, -0.3f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animation/Door/GrandDoor_Open.anim");
            Assert.IsNotNull(clip);
            var paths = new HashSet<string>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip)) paths.Add(binding.path);
            CollectionAssert.AreEquivalent(new[] { "Hinge", "HingeRight" }, paths);
        }
    }
}
