using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// 1 フロア分のレイアウトを生成する入口。
    /// BSP → 部屋グラフ → 役割割り当て → 部屋テンプレート配置 → 廊下カーブ の順に進む。
    /// UnityEngine には依存しないので、EditMode テストからそのまま呼べる。
    /// </summary>
    public static class FloorLayoutGenerator
    {
        public static FloorLayout Generate(FloorGenerationParams parameters, int seed)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            parameters.Validate();

            var rng = new Random(seed);

            var root = BspPartitioner.Partition(parameters, rng);
            var leafRects = BspPartitioner.CollectLeafRects(root);
            var edges = RoomGraphBuilder.Build(root, leafRects, parameters, rng);

            var canHostStair = new bool[leafRects.Count];
            var canHostShop = new bool[leafRects.Count];
            for (int i = 0; i < leafRects.Count; i++)
            {
                canHostStair[i] = RoomPlacer.CanHost(leafRects[i], RoomTag.Stair, parameters);
                canHostShop[i] = RoomPlacer.CanHost(leafRects[i], RoomTag.Shop, parameters);
            }

            var assignment = SpecialRoomAssigner.Assign(leafRects.Count, edges, canHostStair, canHostShop, parameters, rng);

            var roles = new RoomRole[leafRects.Count];
            for (int i = 0; i < roles.Length; i++) roles[i] = RoomRole.Normal;
            if (assignment.StairUpLeaf >= 0) roles[assignment.StairUpLeaf] = RoomRole.StairUp;
            if (assignment.StairDownLeaf >= 0) roles[assignment.StairDownLeaf] = RoomRole.StairDown;
            if (assignment.ShopLeaf >= 0) roles[assignment.ShopLeaf] = RoomRole.Shop;

            var rooms = RoomPlacer.Place(leafRects, roles, parameters, rng);

            var carver = new CorridorCarver(parameters.Width, parameters.Height, rooms, parameters);
            var corridorCells = carver.CarveAll(edges, rooms);

            var layout = new FloorLayout(seed, parameters.Width, parameters.Height, rooms, edges, corridorCells, leafRects);

            // 専用テンプレートが収まらず RoomPlacer が役割を落とした場合でも、
            // 階段とショップはフロアに必ず 1 つ要るので通常部屋のまま役割だけ戻す。
            layout.StairUpRoom = ResolveRole(rooms, assignment.StairUpLeaf, RoomRole.StairUp);
            layout.StairDownRoom = ResolveRole(rooms, assignment.StairDownLeaf, RoomRole.StairDown);
            layout.ShopRoom = parameters.IncludeShop
                ? ResolveRole(rooms, assignment.ShopLeaf, RoomRole.Shop)
                : -1;

            foreach (var room in rooms) room.FeatureCell = PickFeatureCell(room);

            return layout;
        }

        private static int ResolveRole(IReadOnlyList<RoomInstance> rooms, int leafIndex, RoomRole role)
        {
            if (leafIndex < 0 || leafIndex >= rooms.Count) return -1;
            rooms[leafIndex].Role = role;
            return leafIndex;
        }

        /// <summary>階段やショップのマーカーを置くセル。ドアの正面を塞がないよう中心を使う。</summary>
        private static GridPos PickFeatureCell(RoomInstance room) => room.Bounds.Center;
    }

    /// <summary>生成結果を調べるためのヘルパー。テストとデバッグ表示から使う。</summary>
    public static class FloorLayoutAnalysis
    {
        /// <summary>startRoom から廊下づたいに到達できる部屋のフラグ配列。</summary>
        public static bool[] ReachableRooms(FloorLayout layout, int startRoom)
        {
            var reachable = new bool[layout.Rooms.Count];
            if (startRoom < 0 || startRoom >= layout.Rooms.Count) return reachable;

            var adjacency = new List<int>[layout.Rooms.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            foreach (var edge in layout.Edges)
            {
                if (!edge.IsCarved) continue;
                adjacency[edge.RoomA].Add(edge.RoomB);
                adjacency[edge.RoomB].Add(edge.RoomA);
            }

            var queue = new Queue<int>();
            reachable[startRoom] = true;
            queue.Enqueue(startRoom);
            while (queue.Count > 0)
            {
                foreach (int next in adjacency[queue.Dequeue()])
                {
                    if (reachable[next]) continue;
                    reachable[next] = true;
                    queue.Enqueue(next);
                }
            }

            return reachable;
        }

        /// <summary>全部屋が上り階段の部屋から到達できるか。</summary>
        public static bool IsFullyConnected(FloorLayout layout)
        {
            if (layout.Rooms.Count == 0) return true;
            int start = layout.StairUpRoom >= 0 ? layout.StairUpRoom : 0;
            foreach (bool reachable in ReachableRooms(layout, start))
            {
                if (!reachable) return false;
            }
            return true;
        }
    }
}
