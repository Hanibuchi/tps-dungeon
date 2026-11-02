using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// 1 フロア分のレイアウトを生成する入口。
    /// 成長パッキングで部屋と辺を同時に決め、そのあと階段・ショップ・ショートカットの役割を割り当てる。
    /// ショップとショートカットは、置くかどうかを最初に確率（ShopChance・ShortcutChance）で引く。
    /// UnityEngine には依存しないので、EditMode テストからそのまま呼べる。
    /// </summary>
    public static class FloorLayoutGenerator
    {
        public static FloorLayout Generate(FloorGenerationParams parameters, int seed)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            parameters.Validate();
            parameters = Resolve(parameters, seed);

            var rng = new Random(seed);

            // 成長がフロアの隅に突っ込んで早々に行き場を失うシードがあるので、部屋数が
            // 下限に届かないか、階段の部屋が 2 つ揃わなければ作り直す。rng は使い回すため、シードが同じなら結果も同じ。
            // 階段の部屋が足りないと普通の部屋で代用することになるので、部屋数より階段の部屋が揃っていることを優先する。
            bool hasStairTemplate = HasStairTemplate(parameters);
            var packer = new RoomGrowthPacker(parameters, rng);
            var best = packer.Run();
            for (int attempt = 1; attempt < parameters.MaxPackAttempts && !IsGoodEnough(best); attempt++)
            {
                var retry = packer.Run();
                if (IsBetter(retry, best)) best = retry;
            }

            bool HasEnoughStairs(PackResult result) => !hasStairTemplate || CountStairRooms(result) >= 2;
            bool IsGoodEnough(PackResult result) => result.Rooms.Count >= parameters.MinRoomCount && HasEnoughStairs(result);
            bool IsBetter(PackResult a, PackResult b)
            {
                bool aStairs = HasEnoughStairs(a), bStairs = HasEnoughStairs(b);
                if (aStairs != bStairs) return aStairs;
                return a.Rooms.Count > b.Rooms.Count;
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
            var canHostShortcut = new bool[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                canHostStair[i] = (rooms[i].Template.Tags & RoomTag.Stair) != 0;
                canHostShop[i] = (rooms[i].Template.Tags & RoomTag.Shop) != 0;
                canHostShortcut[i] = (rooms[i].Template.Tags & RoomTag.Shortcut) != 0;
            }

            // 階段とショップのマーカーは FloorBuilder が部屋の中心に独立して置くので、
            // 専用テンプレートが足りなくても通常部屋で代用できる。足りないときは全部屋を候補に開放する。
            if (CountTrue(canHostStair) < 2) Fill(canHostStair);
            if (parameters.IncludeShop && CountTrue(canHostShop) < 1) Fill(canHostShop);
            // ショートカットの部屋（井戸の専用の部屋）は階段に使わない。
            for (int i = 0; i < rooms.Count; i++)
            {
                if (canHostShortcut[i]) canHostStair[i] = false;
            }

            var assignment = SpecialRoomAssigner.Assign(rooms.Count, edges, canHostStair, canHostShop, canHostShortcut, parameters, rng);

            var layout = new FloorLayout(seed, parameters.Width, parameters.Height, rooms, edges);
            layout.EntranceRoom = ResolveRole(rooms, assignment.EntranceRoom, RoomRole.Entrance);
            layout.ExitRoom = ResolveRole(rooms, assignment.ExitRoom, RoomRole.Exit);
            layout.ShopRoom = parameters.IncludeShop
                ? ResolveRole(rooms, assignment.ShopRoom, RoomRole.Shop)
                : -1;
            int shortcut = parameters.IncludeShortcut ? ResolveRole(rooms, assignment.ShortcutRoom, RoomRole.Shortcut) : -1;

            foreach (var room in rooms) room.FeatureCell = PickFeatureCell(room);
            // ショートカットの部屋の真ん中に井戸を置く（FloorBuilder）。
            if (shortcut >= 0) layout.ShortcutCell = rooms[shortcut].FeatureCell;

            return layout;
        }

        // 置くかどうかを引く乱数の塩。部屋を詰める乱数とは別の流れにして、確率を変えても間取りの引き方が変わらないようにする。
        private const int RollSalt = 0x5A17C0DE;

        /// <summary>
        /// ショップとショートカットを今回置くかを引き、その結果を入れた生成条件の写しを返す。
        /// ショートカットを置かない回は、Shortcut タグの部屋を候補から外す（普通の部屋として紛れ込まないように）。
        /// </summary>
        private static FloorGenerationParams Resolve(FloorGenerationParams parameters, int seed)
        {
            var roll = new Random(seed ^ RollSalt);
            var resolved = parameters.Clone();
            resolved.IncludeShop = roll.NextDouble() < parameters.ShopChance;
            resolved.IncludeShortcut = roll.NextDouble() < parameters.ShortcutChance;
            if (!resolved.IncludeShortcut)
            {
                var templates = new List<RoomTemplateData>();
                foreach (var template in parameters.Templates)
                {
                    if ((template.Tags & RoomTag.Shortcut) == 0) templates.Add(template);
                }
                resolved.Templates = templates;
            }
            return resolved;
        }

        private static int ResolveRole(IReadOnlyList<RoomInstance> rooms, int roomIndex, RoomRole role)
        {
            if (roomIndex < 0 || roomIndex >= rooms.Count) return -1;
            rooms[roomIndex].Role = role;
            return roomIndex;
        }

        /// <summary>階段やショップのマーカーを置くセル。ドアの正面を塞がないよう中心を使う。</summary>
        private static GridPos PickFeatureCell(RoomInstance room) => room.Bounds.Center;

        /// <summary>階段の部屋のテンプレートが抽選に入っているか。無ければ階段の部屋の数で作り直さない。</summary>
        private static bool HasStairTemplate(FloorGenerationParams parameters)
        {
            foreach (var template in parameters.Templates)
            {
                if (template.Weight > 0f && (template.Tags & RoomTag.Stair) != 0) return true;
            }
            return false;
        }

        private static int CountStairRooms(PackResult result)
        {
            int count = 0;
            foreach (var room in result.Rooms)
            {
                if ((room.Template.Tags & RoomTag.Stair) != 0) count++;
            }
            return count;
        }

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

        /// <summary>全部屋が入口の部屋から到達できるか。</summary>
        public static bool IsFullyConnected(FloorLayout layout)
        {
            if (layout.Rooms.Count == 0) return true;
            int start = layout.EntranceRoom >= 0 ? layout.EntranceRoom : 0;
            foreach (bool reachable in ReachableRooms(layout, start))
            {
                if (!reachable) return false;
            }
            return true;
        }
    }
}
