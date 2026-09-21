using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// 1 フロア分のレイアウトを生成する入口。
    /// 成長パッキングで部屋と辺を同時に決め、そのあと階段とショップの役割を割り当てる。
    /// UnityEngine には依存しないので、EditMode テストからそのまま呼べる。
    /// </summary>
    public static class FloorLayoutGenerator
    {
        public static FloorLayout Generate(FloorGenerationParams parameters, int seed)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            parameters.Validate();

            var rng = new Random(seed);

            // 成長がフロアの隅に突っ込んで早々に行き場を失うシードがあるので、部屋数が
            // 下限に届かなければ作り直す。rng は使い回すため、シードが同じなら結果も同じ。
            var packer = new RoomGrowthPacker(parameters, rng);
            var best = packer.Run();
            for (int attempt = 1; attempt < parameters.MaxPackAttempts && best.Rooms.Count < parameters.MinRoomCount; attempt++)
            {
                var retry = packer.Run();
                if (retry.Rooms.Count > best.Rooms.Count) best = retry;
            }

            if (best.Rooms.Count == 0)
            {
                throw new InvalidOperationException(
                    "部屋を 1 つも置けなかった。フロアサイズに対してテンプレートが大きすぎないか確認すること");
            }

            var rooms = best.Rooms;
            var edges = best.Edges;

            var canHostStair = new bool[rooms.Count];
            var canHostShop = new bool[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                canHostStair[i] = (rooms[i].Template.Tags & RoomTag.Stair) != 0;
                canHostShop[i] = (rooms[i].Template.Tags & RoomTag.Shop) != 0;
            }

            // 階段とショップのマーカーは FloorBuilder が部屋の中心に独立して置くので、
            // 専用テンプレートが足りなくても通常部屋で代用できる。足りないときは全部屋を候補に開放する。
            if (CountTrue(canHostStair) < 2) Fill(canHostStair);
            if (parameters.IncludeShop && CountTrue(canHostShop) < 1) Fill(canHostShop);

            var assignment = SpecialRoomAssigner.Assign(rooms.Count, edges, canHostStair, canHostShop, parameters, rng);

            var layout = new FloorLayout(seed, parameters.Width, parameters.Height, rooms, edges);
            layout.StairUpRoom = ResolveRole(rooms, assignment.StairUpRoom, RoomRole.StairUp);
            layout.StairDownRoom = ResolveRole(rooms, assignment.StairDownRoom, RoomRole.StairDown);
            layout.ShopRoom = parameters.IncludeShop
                ? ResolveRole(rooms, assignment.ShopRoom, RoomRole.Shop)
                : -1;

            foreach (var room in rooms) room.FeatureCell = PickFeatureCell(room);

            return layout;
        }

        private static int ResolveRole(IReadOnlyList<RoomInstance> rooms, int roomIndex, RoomRole role)
        {
            if (roomIndex < 0 || roomIndex >= rooms.Count) return -1;
            rooms[roomIndex].Role = role;
            return roomIndex;
        }

        /// <summary>階段やショップのマーカーを置くセル。ドアの正面を塞がないよう中心を使う。</summary>
        private static GridPos PickFeatureCell(RoomInstance room) => room.Bounds.Center;

        private static int CountTrue(bool[] flags)
        {
            int count = 0;
            foreach (bool flag in flags)
            {
                if (flag) count++;
            }
            return count;
        }

        private static void Fill(bool[] flags)
        {
            for (int i = 0; i < flags.Length; i++) flags[i] = true;
        }
    }

    /// <summary>生成結果を調べるためのヘルパー。テストとデバッグ表示から使う。</summary>
    public static class FloorLayoutAnalysis
    {
        /// <summary>startRoom からドアづたいに到達できる部屋のフラグ配列。</summary>
        public static bool[] ReachableRooms(FloorLayout layout, int startRoom)
        {
            var reachable = new bool[layout.Rooms.Count];
            if (startRoom < 0 || startRoom >= layout.Rooms.Count) return reachable;

            var adjacency = new List<int>[layout.Rooms.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            foreach (var edge in layout.Edges)
            {
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
