using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 入口に下り階段、出口に上り階段が置かれ、そのセルの床・天井・土台が階段に合わせて組まれることを、
    /// 今の FloorConfig で実際に階を組んで確かめる。
    /// </summary>
    public sealed class FloorStairTests
    {
        private const string ConfigPath = "Assets/_Project/Settings/Map/Floor_L1_Config.asset";

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void EntranceAndExit_GetStairsThatReshapeTheirCell(int seed)
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} が無い");

            host = new GameObject("FloorStairTests");
            var builder = host.AddComponent<FloorBuilder>();
            var layout = builder.Build(config, seed);

            var entrance = layout.RoomAt(layout.EntranceRoom);
            var exit = layout.RoomAt(layout.ExitRoom);
            Assert.IsNotNull(entrance, $"seed {seed}: 入口が無い");
            Assert.IsNotNull(exit, $"seed {seed}: 出口が無い");

            // 入口は下の階へ続く下り階段で、調べると移れる（行き先は FloorBootstrap が入れる）。
            Assert.IsNotNull(builder.EntranceStair, $"seed {seed}: 入口に階段が無い");
            Assert.IsNotNull(builder.EntranceStair.GetComponentInChildren<SceneGate>(), "下り階段は調べて移れる");
            Assert.IsTrue(builder.EntranceStair.GetComponent<FloorFeature>().replacesRoomFloor);

            // 出口は上の階へ続く上り階段で、まだ見た目だけ。
            Assert.IsNotNull(builder.ExitStair, $"seed {seed}: 出口に階段が無い");
            Assert.IsNull(builder.ExitStair.GetComponentInChildren<SceneGate>(), "上り階段は見た目だけ");

            // 入口の部屋の床は階段の床（穴の空いた床）に置き換わる。
            var entranceRoom = FindRoom(builder, entrance.Index);
            if (entrance.Bounds.Width == 1 && entrance.Bounds.Height == 1)
            {
                var floor = entranceRoom.transform.Find("Floor");
                Assert.IsTrue(floor == null || !floor.gameObject.activeSelf, $"seed {seed}: 入口の部屋の床が残っている");
            }

            // 出口のセルには普通の天井を張らない（階段が穴の空いた天井を持つ）。
            var exitCell = exit.FeatureCell;
            Assert.IsNull(builder.CeilingRoot.Find($"Ceiling_{exitCell.X}_{exitCell.Y}"), $"seed {seed}: 出口のセルに天井が張られている");

            // 入口のセルの土台は竪穴の底まで下げる。
            var entranceCell = entrance.FeatureCell;
            var foundation = builder.GeneratedRoot.Find($"Foundation/Foundation_{entranceCell.X}_{entranceCell.Y}");
            if (foundation != null)
            {
                float depth = builder.EntranceStair.GetComponent<FloorFeature>().depthBelowFloor;
                Assert.AreEqual(-depth, foundation.localPosition.y, 0.001f, "入口のセルの土台は竪穴の底から");
            }
        }

        private static GameObject FindRoom(FloorBuilder builder, int index)
        {
            var rooms = builder.GeneratedRoot.Find("Rooms");
            foreach (Transform room in rooms)
            {
                if (room.name.StartsWith($"Room{index}_")) return room.gameObject;
            }
            Assert.Fail($"部屋 {index} の実体が無い");
            return null;
        }
    }
}
