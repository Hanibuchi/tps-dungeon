using System.Linq;
using NUnit.Framework;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Map.Tests
{
    /// <summary>
    /// 組んだ階のドアがどれも辺（両側の部屋）と結び付いていて、部屋ごとにそのドアを引けること（部屋の敵を出す・モンスターハウスに閉じ込めるのに使う）と、
    /// 閉じ込めるときの Seal で閉ざされ、Unlock で戻ることを確かめる。
    /// </summary>
    public sealed class RoomDoorsTests
    {
        private const string ConfigPath = "Assets/_Project/Settings/Map/Floor_L1_Config.asset";
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(1)]
        [TestCase(42)]
        public void EveryDoorKnowsItsRooms(int seed)
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} が無い");

            host = new GameObject("RoomDoorsTests");
            var builder = host.AddComponent<FloorBuilder>();
            var layout = builder.Build(config, seed);

            var doors = builder.GeneratedRoot.GetComponentsInChildren<Door>(true);
            Assert.AreEqual(layout.Edges.Count, doors.Length, "辺ごとにドアが 1 枚");
            foreach (var door in doors)
            {
                Assert.IsTrue(builder.TryGetEdge(door, out var edge), $"{door.name} の辺が分からない");
                // ドアは 2 部屋の境界の上にある：少しずらすと片側ずつの部屋のセルに乗る。
                var a = layout.RoomAt(edge.RoomA);
                var cell = builder.WorldToCell(builder.transform.TransformPoint(builder.CellCenter(edge.SocketA.Cell)));
                Assert.IsTrue(a.Bounds.Contains(cell));
                Assert.IsTrue(layout.RoomAt(edge.RoomB).Bounds.Contains(edge.SocketB.Cell));
            }

            foreach (var room in layout.Rooms)
            {
                int expected = layout.Edges.Count(e => e.RoomA == room.Index || e.RoomB == room.Index);
                Assert.AreEqual(expected, builder.DoorsOf(room.Index).Count(), $"{room} のドアの数");
            }

            Assert.IsFalse(builder.TryGetEdge(null, out _));
        }

        [Test]
        public void Seal_LocksUntilUnlocked()
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(ConfigPath);
            host = new GameObject("RoomDoorsTests");
            var builder = host.AddComponent<FloorBuilder>();
            builder.Build(config, 7);

            var door = builder.GeneratedRoot.GetComponentInChildren<Door>();
            Assert.IsFalse(door.IsLocked);
            door.Seal();
            Assert.IsTrue(door.IsLocked);
            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual("閉ざされている", door.PromptLabel);
            door.Interact(null);
            Assert.IsFalse(door.IsOpen, "閉ざされている間は開かない");
            door.Unlock();
            Assert.IsFalse(door.IsLocked);
            Assert.AreNotEqual("閉ざされている", door.PromptLabel);
        }
    }
}
