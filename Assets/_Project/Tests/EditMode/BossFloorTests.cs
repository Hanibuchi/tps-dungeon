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
    /// ボス階を設計図（BossFloors.md）のとおりに組むこと（部屋・扉・敵の出る部屋・ショートカットの部屋）と、
    /// それを今の設定（Floor_L*_Boss_Config）で組んだときに大扉・閉ざされた扉・井戸が揃うこと、大扉が両側へ開くことを確かめる。
    /// </summary>
    public sealed class BossFloorTests
    {
        private static RoomInstance RoomWithId(FloorLayout layout, string id)
        {
            foreach (var room in layout.Rooms)
            {
                if (room.Template.Id == id) return room;
            }
            return null;
        }

        private static RoomEdge EdgeBetween(FloorLayout layout, RoomInstance a, RoomInstance b)
        {
            foreach (var edge in layout.Edges)
            {
                if ((edge.RoomA == a.Index && edge.RoomB == b.Index) || (edge.RoomA == b.Index && edge.RoomB == a.Index)) return edge;
            }
            return null;
        }

        [Test]
        public void Blueprint_ReadsRoomsDoorsAndEnemies()
        {
            var errors = new List<string>();
            var blueprint = BossFloorBlueprint.Parse(FloorGenerationFixture.SimpleBossBlueprint, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(new GridRect(3, 0, 1, 1), blueprint.Rooms['E'], "一番下の行が Y 0");
            Assert.AreEqual(new GridRect(2, 1, 3, 1), blueprint.Rooms['H']);
            Assert.AreEqual(new GridRect(5, 1, 1, 1), blueprint.Rooms['S']);
            Assert.AreEqual(new GridRect(1, 2, 5, 5), blueprint.Rooms['A']);
            Assert.AreEqual(new GridRect(3, 7, 1, 1), blueprint.Rooms['X']);
            Assert.IsTrue(blueprint.HasDoor('H', 'E'));
            Assert.IsTrue(blueprint.IsEnemyRoom('H'));
            Assert.IsFalse(blueprint.IsEnemyRoom('A'));
        }

        [TestCase("..X..\n.AAAAA\n", "ボス部屋")]
        [TestCase("aa.a\n", "四角形")]
        [TestCase("...X...\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n..HHHS.\n...E...\nB\n", "大文字")]
        [TestCase("...X...\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n..HHHS.\n...E...\n扉: E-S\n", "接していない")]
        [TestCase("...X...\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n.AAAAA.\n..HHHS.\n...E...\n扉: E-H\n敵: z\n", "地図に無い")]
        [TestCase("...X...\nこれは何\n", "読めない行")]
        public void Blueprint_ReportsMistakes(string body, string expected)
        {
            var errors = new List<string>();
            BossFloorBlueprint.Parse(body, errors);
            Assert.IsTrue(errors.Exists(e => e.Contains(expected)), $"「{expected}」の知らせが無い: {string.Join(" / ", errors)}");
        }

        [Test]
        public void Blueprint_SplitsTheFileByLayer()
        {
            string text = "> 説明\n# 第1層\n" + FloorGenerationFixture.SimpleBossBlueprint + "\n# 第 2 層\n" + FloorGenerationFixture.SimpleBossBlueprint;
            var errors = new List<string>();
            var blueprints = BossFloorBlueprint.ParseFile(text, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(2, blueprints.Count);
            Assert.AreEqual(1, blueprints[0].Layer);
            Assert.AreEqual(2, blueprints[1].Layer);
            Assert.AreEqual(2, blueprints[0].Line);
            StringAssert.StartsWith("...X...", blueprints[1].Body);
        }

        [Test]
        public void Layout_FollowsTheBlueprint()
        {
            var layout = FloorGenerationFixture.BossLayout(FloorGenerationFixture.SimpleBossBlueprint);
            Assert.AreEqual(5, layout.Rooms.Count);
            var entrance = layout.RoomAt(layout.EntranceRoom);
            var exit = layout.RoomAt(layout.ExitRoom);
            var hall = RoomWithId(layout, "Room_BossHall_3x1");
            var arena = RoomWithId(layout, BossFloorLayout.ArenaId);
            var shortcut = RoomWithId(layout, BossFloorLayout.ShortcutId);

            // 部屋の並びは設計図のまま、全体をグリッドの真ん中に寄せる（7x8 を 16x16 の真ん中へ）。
            Assert.AreEqual(new GridRect(5, 6, 5, 5), arena.Bounds);
            Assert.AreEqual(new GridRect(6, 5, 3, 1), hall.Bounds);
            Assert.AreEqual(new GridRect(9, 5, 1, 1), shortcut.Bounds);
            Assert.AreEqual(new GridRect(7, 4, 1, 1), entrance.Bounds);
            Assert.AreEqual(new GridRect(7, 11, 1, 1), exit.Bounds);
            Assert.AreEqual(shortcut.Bounds.Center, layout.ShortcutCell);
            Assert.IsNull(layout.MirrorColumn);

            // 決まった扉：大扉（H-A、ボス部屋の真ん中の列）と、ボスを倒すまで閉ざされた出口の扉（A-X）。S は書いていないので H と繋ぐ。
            var grand = EdgeBetween(layout, hall, arena);
            Assert.IsTrue(grand.IsGrandDoor);
            Assert.AreEqual(arena.Bounds.Center.X, grand.SocketA.Cell.X);
            Assert.IsTrue(EdgeBetween(layout, arena, exit).IsLocked);
            Assert.IsTrue(EdgeBetween(layout, hall, shortcut).IsShortcutDoor);
            Assert.IsNotNull(EdgeBetween(layout, entrance, hall));
            Assert.AreEqual(4, layout.Edges.Count);

            // 階段の部屋はドアの向きへ回す（入口は北の H へ、出口は南の A へ）。ショートカットの部屋は西の H へ（回さない）。
            Assert.AreEqual(2, entrance.Rotation);
            Assert.AreEqual(0, exit.Rotation);
            Assert.AreEqual(0, shortcut.Rotation);

            // 敵は設計図に書いた部屋だけ。
            foreach (var room in layout.Rooms) Assert.AreEqual(room == hall, RoomEnemyRoller.CanHostEnemies(room), $"{room}");
            Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout));
        }

        [Test]
        public void Layout_TurnsTheShortcutRoomToItsDoor()
        {
            // ショートカットの部屋が道のりの部屋 c の上にあり、扉は南。
            const string body =
                "......X......\n" +
                "....AAAAA....\n" +
                "....AAAAA....\n" +
                "....AAAAA....\n" +
                "....AAAAA....\n" +
                "...EAAAAAS...\n" +
                ".aaaaHHHcccc.\n" +
                ".aaaa...cccc.\n" +
                "扉: E-a a-H c-H c-S\n" +
                "敵: a c H\n";
            var layout = FloorGenerationFixture.BossLayout(body);
            var shortcut = RoomWithId(layout, BossFloorLayout.ShortcutId);
            RoomEdge edge = null;
            foreach (var e in layout.Edges) if (e.IsShortcutDoor) edge = e;
            var other = layout.RoomAt(edge.Other(shortcut.Index));
            Assert.AreEqual("Room_BossPath_4x2", other.Template.Id, "ショートカットの部屋は c と繋がる");
            Assert.Greater(other.Bounds.X, shortcut.Bounds.X - 2, "東側の c（西の a ではない）");
            Assert.AreEqual(3, shortcut.Rotation, "西のドア候補を南へ回す");
            var entrance = layout.RoomAt(layout.EntranceRoom);
            Assert.AreEqual(0, entrance.Rotation, "入口のドアは南の a へ（階段の部屋はもともと南向き）");
            Assert.IsNull(EdgeBetween(layout, entrance, RoomWithId(layout, BossFloorLayout.ArenaId)), "接していても書いていなければ扉は無い");
            Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout));
        }

        [Test]
        public void Layout_RejectsRoomsThatCannotBeReached()
        {
            const string body =
                "...X...\n" +
                ".AAAAA.\n" +
                ".AAAAA.\n" +
                ".AAAAA.\n" +
                ".AAAAA.\n" +
                ".AAAAA.\n" +
                "..HHHS.\n" +
                "...Eb..\n";
            Assert.Throws<System.ArgumentException>(() => FloorGenerationFixture.BossLayout(body), "扉の無い入口は H と繋がらない");
        }

        [Test]
        public void Layout_ShapeDoesNotDependOnSeed()
        {
            string a = FloorGenerationFixture.BossLayout(FloorGenerationFixture.SimpleBossBlueprint, 1).ComputeSignature();
            string b = FloorGenerationFixture.BossLayout(FloorGenerationFixture.SimpleBossBlueprint, 999).ComputeSignature();
            Assert.AreEqual(a.Substring(a.IndexOf('|')), b.Substring(b.IndexOf('|')));
        }

        [Test]
        public void ShippedBlueprints_BuildEveryLayer()
        {
            var errors = new List<string>();
            var blueprints = BossFloorBlueprint.ParseFile(System.IO.File.ReadAllText("Assets/_Project/Settings/Tables/BossFloors.md"), errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(5, blueprints.Count);
            foreach (var blueprint in blueprints)
            {
                var config = AssetDatabase.LoadAssetAtPath<FloorConfig>($"Assets/_Project/Settings/Map/Floor_L{blueprint.Layer}_Boss_Config.asset");
                Assert.AreEqual(blueprint.Body, config.bossBlueprint, $"第 {blueprint.Layer} 層の設計図が取り込まれていない");
                var layout = config.GenerateBossLayout(1);
                Assert.IsTrue(FloorLayoutAnalysis.IsFullyConnected(layout));
                Assert.AreEqual(blueprint.Rooms.Count, layout.Rooms.Count);
                int enemyRooms = 0;
                foreach (var room in layout.Rooms) if (RoomEnemyRoller.CanHostEnemies(room)) enemyRooms++;
                Assert.AreEqual(blueprint.EnemyRooms.Count, enemyRooms, $"第 {blueprint.Layer} 層の敵の出る部屋");
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
        [TestCase(2, 42)]
        [TestCase(3, 7)]
        [TestCase(4, 9)]
        [TestCase(5, 777)]
        public void BossFloor_BuildsGrandDoorLockedShortcutRoomAndWell(int layer, int seed)
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
            foreach (Transform piece in walls)
            {
                if (piece.name.StartsWith("GrandDoor_")) grand++;
                else if (piece.name.StartsWith("Door_")) doors++;
            }
            Assert.AreEqual(1, grand, "大扉は 1 枚");
            Assert.AreEqual(layout.Edges.Count - 1, doors, "大扉のほかは普通のドア");

            // 出口の扉はボスを倒すまで閉ざされていて、調べても開かない。UnlockDoors で開けられるようになる。
            Assert.AreEqual(1, builder.LockedDoors.Count);
            var gate = builder.LockedDoors[0];
            Assert.IsTrue(gate.IsLocked);
            Assert.AreEqual("閉ざされている", gate.PromptLabel);
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

            Assert.IsNotNull(builder.ShortcutMarker, "ショートカットの井戸がある");
            Assert.AreEqual(layout.ShortcutCell.Value, builder.WorldToCell(builder.ShortcutMarker.transform.position));
            Assert.IsNotNull(builder.ShortcutWell, "井戸に調べる所（ShortcutPoint）がある");
            Assert.IsFalse(builder.ShortcutWell.IsOpen, "解放していない層の井戸は閉じている");
            Assert.IsFalse(builder.ShortcutWell.CanInteract(null), "閉じている井戸は扉の隙間から照準が届いても調べられない");
            var spawn = builder.GeneratedRoot.Find("Features/ShortcutSpawn");
            Assert.IsNotNull(spawn, "地上から登ってきたパーティーが立つ所がある");
            Assert.AreEqual(layout.ShortcutCell.Value, builder.WorldToCell(spawn.position), "ショートカットの部屋に立つ");
            // 扉の方を向く（井戸は部屋と一緒に回してある）。
            var door = builder.ShortcutDoor.transform.position - spawn.position;
            Assert.Greater(Vector3.Dot(spawn.forward, new Vector3(door.x, 0f, door.z).normalized), 0.7f, "扉の方を向いて立つ");
            Assert.IsNotNull(builder.EntranceStair);
            Assert.IsNotNull(builder.ExitStair);
            Assert.AreEqual(layout.RoomAt(layout.ExitRoom).FeatureCell, builder.WorldToCell(builder.ExitStair.transform.position));
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
