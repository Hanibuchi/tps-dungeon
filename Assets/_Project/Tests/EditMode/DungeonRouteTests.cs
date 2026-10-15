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
        }

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
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            Assert.IsNotNull(route, $"{RoutePath} が無い");

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
            Assert.AreEqual("戻れない", down.PromptLabel);

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

            var up = host.GetComponent<FloorBuilder>().ExitStair.GetComponentInChildren<SceneGate>();
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
