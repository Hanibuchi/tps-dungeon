using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// ダンジョンの道順（層ごとに ランダムな階 × n → ボス階）の並びと、階のシーンが道順に従って階段を繋ぐことを確かめる。
    /// </summary>
    public sealed class DungeonRouteTests
    {
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            DungeonProgress.Pending = null;
            DungeonProgress.Current = null;
            ShortcutUnlocks.PathOverride = null;
            if (System.IO.File.Exists(unlockPath)) System.IO.File.Delete(unlockPath);
        }

        private string unlockPath => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tps-dungeon-tests-shortcuts.json");

        private DungeonRoute Route(params int[] randomCounts)
        {
            var route = ScriptableObject.CreateInstance<DungeonRoute>();
            created.Add(route);
            var layers = new List<DungeonRoute.Layer>();
            for (int i = 0; i < randomCounts.Length; i++)
            {
                layers.Add(new DungeonRoute.Layer
                {
                    randomScene = $"L{i + 1}_Random",
                    randomFloorCount = randomCounts[i],
                    bossScene = $"L{i + 1}_Boss",
                });
            }
            route.SetLayers(layers);
            return route;
        }

        private static List<string> Walk(DungeonRoute route)
        {
            var visited = new List<string>();
            for (var floor = route.First; floor.HasValue; floor = route.Next(floor.Value))
            {
                visited.Add($"{route.SceneOf(floor.Value)}:{floor.Value.Floor + 1}");
                Assert.Less(visited.Count, 100, "道順が終わらない");
            }
            return visited;
        }

        [Test]
        public void Next_ClimbsRandomFloorsThenBossThenNextLayer()
        {
            var route = Route(2, 3);
            CollectionAssert.AreEqual(new[]
            {
                "L1_Random:1", "L1_Random:2", "L1_Boss:3",
                "L2_Random:1", "L2_Random:2", "L2_Random:3", "L2_Boss:4",
            }, Walk(route));
            Assert.IsNull(route.Next(new DungeonFloor(1, 3)), "最後の階の上は無い");
        }

        [Test]
        public void Previous_IsTheReverseOfNext()
        {
            var route = Route(4, 4, 4);
            for (var floor = route.First; floor.HasValue; floor = route.Next(floor.Value))
            {
                var next = route.Next(floor.Value);
                if (next.HasValue) Assert.AreEqual(floor, route.Previous(next.Value));
            }
            Assert.IsNull(route.Previous(route.First.Value), "最初の階の下は無い（地上）");
        }

        [Test]
        public void LayerWithoutRandomFloorsOrBoss_IsSkippedOrShortened()
        {
            var route = Route(0, 1);
            var layers = new List<DungeonRoute.Layer>(route.Layers);
            layers[1].bossScene = "";
            route.SetLayers(layers);
            CollectionAssert.AreEqual(new[] { "L1_Boss:1", "L2_Random:1" }, Walk(route));
            Assert.AreEqual(new DungeonFloor(0, 0), route.FirstFloorOfScene("L1_Boss"));
            Assert.IsNull(route.FirstFloorOfScene("L1_Random"), "ランダムな階が無い層のシーンでは始まらない");
        }

        [Test]
        public void FirstFloorOfScene_AndLabels()
        {
            var route = Route(4, 4);
            Assert.AreEqual(new DungeonFloor(1, 0), route.FirstFloorOfScene("L2_Random"));
            Assert.AreEqual(new DungeonFloor(1, 4), route.FirstFloorOfScene("L2_Boss"));
            Assert.IsNull(route.FirstFloorOfScene("Player_Debug"));
            Assert.IsTrue(route.IsBoss(new DungeonFloor(1, 4)));
            Assert.AreEqual("第 2 層 3 階", route.Label(new DungeonFloor(1, 2)));
            Assert.AreEqual("第 2 層 ボス", route.Label(new DungeonFloor(1, 4)));
            CollectionAssert.AreEqual(new[] { "L1_Random", "L1_Boss", "L2_Random", "L2_Boss" }, new List<string>(route.SceneNames()));
        }

        [Test]
        public void ShippedRoute_HasFiveLayersOfFourRandomFloorsAndABoss_RisingHigher()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            Assert.IsNotNull(route, $"{RoutePath} が無い");
            Assert.AreEqual(5, route.Layers.Count);

            float previous = float.NegativeInfinity;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                var layer = route.Layers[i];
                Assert.AreEqual($"L{i + 1}_Random", layer.randomScene);
                Assert.AreEqual($"L{i + 1}_Boss", layer.bossScene);
                Assert.AreEqual(4, layer.randomFloorCount);
                Assert.IsNotNull(layer.randomConfig);
                Assert.IsNotNull(layer.bossConfig);
                Assert.AreEqual(FloorLayoutShape.Boss, layer.bossConfig.layoutShape);
                Assert.AreEqual(layer.randomConfig.elevation, layer.bossConfig.elevation, "同じ層の階は同じ高さ");
                Assert.Greater(layer.randomConfig.elevation, previous, "上の層ほど高い");
                previous = layer.randomConfig.elevation;

                // シーンが Build Settings に入っていて、道順から読める。
                foreach (string scene in new[] { layer.randomScene, layer.bossScene })
                {
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/_Project/Scenes/{scene}.unity"), $"{scene} が無い");
                    Assert.IsTrue(System.Array.Exists(EditorBuildSettings.scenes, s => s.enabled && s.path.EndsWith($"/{scene}.unity")), $"{scene} が Build Settings に無い");
                }
            }
        }

        [Test]
        public void Bootstrap_WithRoute_ClosesDownStairAndLeadsUpToTheNextFloor()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            Assert.IsNotNull(shipped, $"{RoutePath} が無い");
            // 「下り階段で降りられる」はデバッグのウィンドウで入れたままのことがあるので、複製で切っておく。
            var route = Object.Instantiate(shipped);
            created.Add(route);
            route.AllowGoingDown = false;

            // L2 の 4 階（ランダムな階の最後）に着いた体で作る。上り階段はボス階へ。
            var here = new DungeonFloor(1, 3);
            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var bootstrap = MakeBootstrap(host, route);

            // テストのシーンは名前が道順に載っていないので、着いた階を直に渡す。
            bootstrap.ArriveAt(here);
            bootstrap.Generate(1);

            Assert.AreEqual(here, bootstrap.CurrentFloor);
            Assert.AreEqual(here, DungeonProgress.Current);
            Assert.AreSame(route.ConfigOf(here), bootstrap.Config, "その階の設定で作る");

            var builder = host.GetComponent<FloorBuilder>();
            var down = builder.EntranceStair.GetComponentInChildren<SceneGate>();
            Assert.IsFalse(down.IsOpen, "下へは戻れない");
            Assert.IsFalse(down.CanInteract(null), "閉じた下り階段は案内も出さない");

            var up = builder.ExitStair.GetComponentInChildren<SceneGate>();
            var next = route.Next(here).Value;
            Assert.IsTrue(up.IsOpen);
            Assert.AreEqual(route.SceneOf(next), up.SceneName);

            string requested = null;
            var previous = SceneTravel.Handler;
            SceneTravel.Handler = name => requested = name;
            try
            {
                up.Interact(null);
            }
            finally
            {
                SceneTravel.Handler = previous;
            }
            Assert.AreEqual(route.SceneOf(next), requested);
            Assert.AreEqual(next, DungeonProgress.Pending, "上ると次の階が Pending に入る");
        }

        [Test]
        public void Bootstrap_WhenGoingDownIsAllowed_DownStairLeadsToThePreviousFloor()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            // 出荷の道順を書き換えないよう、複製に設定を入れる。
            var route = Object.Instantiate(shipped);
            created.Add(route);
            route.AllowGoingDown = true;

            var here = new DungeonFloor(1, 0);
            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var bootstrap = MakeBootstrap(host, route);
            bootstrap.ArriveAt(here);
            bootstrap.Generate(1);

            var down = host.GetComponent<FloorBuilder>().EntranceStair.GetComponentInChildren<SceneGate>();
            var previous = route.Previous(here).Value;
            Assert.IsTrue(down.IsOpen);
            Assert.AreEqual("降りる", down.PromptLabel);
            Assert.AreEqual(route.SceneOf(previous), down.SceneName, "2 層 1 階の下は 1 層のボス階");

            string requested = null;
            var handler = SceneTravel.Handler;
            SceneTravel.Handler = name => requested = name;
            try
            {
                down.Interact(null);
            }
            finally
            {
                SceneTravel.Handler = handler;
            }
            Assert.AreEqual(route.SceneOf(previous), requested);
            Assert.AreEqual(previous, DungeonProgress.Pending);
            Assert.IsTrue(DungeonProgress.PendingFromAbove, "降りた先では出口（上り階段）の脇に立つ");

            // 切り替えたら繋ぎ直せる（デバッグのウィンドウから）。
            route.AllowGoingDown = false;
            bootstrap.ReconnectStairs();
            Assert.IsFalse(down.IsOpen);
            Assert.IsFalse(down.CanInteract(null));

            // 1 層 1 階の下は地上。
            Assert.IsNull(route.Previous(route.First.Value));
        }

        [Test]
        public void BossFloor_ExitStairStaysClosedUntilTheDoorsAreUnlocked()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            var boss = new DungeonFloor(0, route.Layers[0].randomFloorCount);
            Assert.IsTrue(route.IsBoss(boss));

            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var bootstrap = MakeBootstrap(host, route);
            bootstrap.ArriveAt(boss);
            bootstrap.Generate(1);

            var builder = host.GetComponent<FloorBuilder>();
            Assert.IsTrue(builder.HasLockedDoors);
            var up = builder.ExitStair.GetComponentInChildren<SceneGate>();
            // 閉ざされた扉の隙間から照準が届いても、調べられず案内も出ない。
            Assert.IsFalse(up.IsOpen, "ボスを倒すまでは上り階段を使えない");
            Assert.IsFalse(up.CanInteract(null), "案内も出さない");

            builder.UnlockDoors();
            Assert.IsFalse(builder.HasLockedDoors);
            Assert.IsTrue(up.IsOpen, "扉を開けられるようになったら上り階段も開く");
            Assert.AreEqual(route.SceneOf(route.Next(boss).Value), up.SceneName);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void BossFloor_ShortcutRoomOpensOnlyOnceItsLayerIsUnlocked(int layer)
        {
            ShortcutUnlocks.PathOverride = unlockPath;
            if (System.IO.File.Exists(unlockPath)) System.IO.File.Delete(unlockPath);

            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            var boss = new DungeonFloor(layer, route.Layers[layer].randomFloorCount);
            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var bootstrap = MakeBootstrap(host, route);
            bootstrap.ArriveAt(boss);
            bootstrap.Generate(1);

            var builder = host.GetComponent<FloorBuilder>();
            Assert.IsTrue(builder.ShortcutDoor.IsLocked, "解放していない層のショートカットの部屋は閉ざされている");
            Assert.AreEqual(layer, builder.ShortcutWell.Layer, "井戸に今の層が入る");
            Assert.IsFalse(builder.ShortcutWell.IsOpen);
            Assert.IsFalse(builder.ShortcutWell.CanInteract(null), "扉の隙間から照準が届いても、井戸の案内は出ない");

            // ほかの層を解放しても開かない。
            ShortcutUnlocks.SetUnlocked(layer + 1, true);
            bootstrap.Generate(1);
            Assert.IsTrue(builder.ShortcutDoor.IsLocked);

            ShortcutUnlocks.SetUnlocked(layer, true);
            ShortcutUnlocks.Reload();
            Assert.IsTrue(ShortcutUnlocks.IsUnlocked(layer), "解放状態はファイルに残る");
            bootstrap.Generate(1);
            Assert.IsFalse(builder.ShortcutDoor.IsLocked, "解放した層ではショートカットの部屋の扉を開けられる");
            Assert.IsTrue(builder.ShortcutWell.IsOpen);
            Assert.AreEqual("ロープを伝って降りる", builder.ShortcutWell.PromptLabel);
            Assert.IsTrue(builder.HasLockedDoors, "出口の扉はボスを倒すまで閉ざされたまま");
        }

        [Test]
        public void ShippedRoute_BossFloorEnemiesFollowTheBossConfig()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            bool any = false;
            for (int i = 0; i < route.Layers.Count; i++)
            {
                var layer = route.Layers[i];
                var boss = new DungeonFloor(i, layer.randomFloorCount);
                var expected = layer.bossConfig.bossPathEnemies ? layer.enemySpawns : null;
                Assert.AreEqual(expected, route.EnemySpawnsOf(boss), $"層 {i + 1} のボス階の敵の表");
                any |= layer.bossConfig.bossPathEnemies;
            }
            Assert.IsTrue(any, "道のりに敵が出るボス階がある");
            Assert.IsFalse(route.Layers[0].bossConfig.bossPathEnemies, "1 層のボス階には道のりが無い");
        }

        [Test]
        public void IsBelow_ComparesLayerThenFloor()
        {
            Assert.IsTrue(DungeonProgress.IsBelow(new DungeonFloor(0, 4), new DungeonFloor(1, 0)));
            Assert.IsTrue(DungeonProgress.IsBelow(new DungeonFloor(1, 1), new DungeonFloor(1, 2)));
            Assert.IsFalse(DungeonProgress.IsBelow(new DungeonFloor(1, 2), new DungeonFloor(1, 2)));
            Assert.IsFalse(DungeonProgress.IsBelow(new DungeonFloor(2, 0), new DungeonFloor(1, 4)));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void ArrivingFromAbove_StandsBesideTheUpStair(int floorIndex)
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            var here = new DungeonFloor(0, floorIndex);

            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var builder = host.AddComponent<FloorBuilder>();
            builder.SpawnAtExit = true;
            builder.Build(route.ConfigOf(here), 3);

            var spawn = builder.GeneratedRoot.Find("Features/PlayerSpawn");
            var beside = builder.ExitStair.transform.Find("SpawnPoint");
            Assert.IsNotNull(beside, "上り階段に SpawnPoint がある");
            // 段の途中（床より上の坂の上、段の幅の真ん中）で、降りる向き（ドアのある -Z）を向く。
            Assert.Greater(beside.localPosition.y, 0.5f, "上り階段の途中");
            Assert.AreEqual(0f, beside.localPosition.x, 0.01f);
            Assert.Less(Vector3.Dot(beside.forward, builder.ExitStair.transform.forward), -0.99f);
            var below = builder.EntranceStair.transform.Find("SpawnPoint");
            Assert.Less(below.localPosition.y, -0.3f, "下り階段の途中（床より下の段の上）");
            Assert.AreEqual(0f, below.localPosition.x, 0.01f);
            Assert.Less(Vector3.Distance(beside.position, spawn.position), 0.001f, "上り階段の脇に立つ");
            Assert.AreEqual(builder.CurrentLayout.RoomAt(builder.CurrentLayout.ExitRoom).FeatureCell, builder.WorldToCell(spawn.position), "出口の部屋");

            // 普段（下から上ってきたとき）は入口の下り階段の脇。
            builder.SpawnAtExit = false;
            builder.Build(route.ConfigOf(here), 3);
            spawn = builder.GeneratedRoot.Find("Features/PlayerSpawn");
            Assert.Less(Vector3.Distance(builder.EntranceStair.transform.Find("SpawnPoint").position, spawn.position), 0.001f);
        }

        [Test]
        public void Bootstrap_OnTheLastFloor_ClosesTheUpStair()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            var last = new DungeonFloor(route.Layers.Count - 1, route.Layers[route.Layers.Count - 1].FloorCount - 1);
            Assert.IsNull(route.Next(last));

            var host = new GameObject("DungeonRouteTests");
            created.Add(host);
            var bootstrap = MakeBootstrap(host, route);
            bootstrap.ArriveAt(last);
            bootstrap.Generate(1);

            var builder = host.GetComponent<FloorBuilder>();
            var up = builder.ExitStair.GetComponentInChildren<SceneGate>();
            Assert.IsFalse(up.IsOpen);
            // 最後の階はボス階で、ボスを倒すまでは案内も出さない。倒したら「この先はまだ無い」。
            builder.UnlockDoors();
            Assert.IsFalse(up.IsOpen);
            Assert.AreEqual("この先はまだ無い", up.PromptLabel);
        }

        private static FloorBootstrap MakeBootstrap(GameObject host, DungeonRoute route)
        {
            host.AddComponent<FloorBuilder>();
            var bootstrap = host.AddComponent<FloorBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("route").objectReferenceValue = route;
            serialized.FindProperty("generateOnStart").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return bootstrap;
        }
    }
}
