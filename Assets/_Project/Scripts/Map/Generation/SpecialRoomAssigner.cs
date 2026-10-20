using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>入口・出口・ショップ・ショートカットをどの部屋に置くかを決める。</summary>
    public static class SpecialRoomAssigner
    {
        public sealed class Assignment
        {
            public int EntranceRoom = -1;
            public int ExitRoom = -1;
            public int ShopRoom = -1;
            public int ShortcutRoom = -1;
        }

        /// <param name="canHostStair">その部屋が階段を置けるテンプレートか。</param>
        /// <param name="canHostShop">その部屋がショップを置けるテンプレートか。</param>
        /// <param name="canHostShortcut">その部屋がショートカット（井戸）を置けるテンプレートか。</param>
        public static Assignment Assign(
            int roomCount,
            IReadOnlyList<RoomEdge> edges,
            bool[] canHostStair,
            bool[] canHostShop,
            bool[] canHostShortcut,
            FloorGenerationParams p,
            Random rng)
        {
            var result = new Assignment();
            if (roomCount == 0) return result;

            var adjacency = BuildAdjacency(roomCount, edges);

            // 階段はグラフ距離が最も離れた 2 部屋に置き、フロアを端から端まで歩かせる。
            int bestA = -1, bestB = -1, bestDistance = -1;
            var distanceBuffer = new int[roomCount];
            for (int a = 0; a < roomCount; a++)
            {
                if (!canHostStair[a]) continue;
                BreadthFirst(a, adjacency, distanceBuffer);
                for (int b = 0; b < roomCount; b++)
                {
                    if (b == a || !canHostStair[b]) continue;
                    int distance = distanceBuffer[b];
                    if (distance < 0) continue;
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (bestA < 0)
            {
                // 階段を置ける部屋が足りない場合は先頭 2 つで妥協する。
                bestA = 0;
                bestB = roomCount > 1 ? 1 : -1;
                bestDistance = 1;
            }

            // どちらを上りにするかはシードで入れ替える。
            if (rng.Next(2) == 0)
            {
                result.EntranceRoom = bestA;
                result.ExitRoom = bestB;
            }
            else
            {
                result.EntranceRoom = bestB;
                result.ExitRoom = bestA;
            }

            if (p.IncludeShop) result.ShopRoom = PickShop(roomCount, adjacency, distanceBuffer, canHostShop, canHostShortcut, bestDistance, result);
            if (p.IncludeShortcut) result.ShortcutRoom = PickShortcut(roomCount, canHostShortcut, result, rng);
            return result;
        }

        /// <summary>
        /// ショートカットは、井戸を置ける専用の部屋（1x1・扉 1 枚）のうち、入口・出口・ショップでない所を乱数で 1 つ。
        /// 専用の部屋が無ければ置かない（-1）。ほかの部屋では井戸の穴が床と合わないので代用しない。
        /// </summary>
        private static int PickShortcut(int roomCount, bool[] canHostShortcut, Assignment taken, Random rng)
        {
            var candidates = new List<int>();
            for (int i = 0; i < roomCount; i++)
            {
                if (!canHostShortcut[i] || i == taken.EntranceRoom || i == taken.ExitRoom || i == taken.ShopRoom) continue;
                candidates.Add(i);
            }
            return candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : -1;
        }

        /// <summary>ショップは入口と出口のちょうど中間あたりに置く。ショートカットの部屋（井戸の専用の部屋）には置かない。</summary>
        private static int PickShop(int roomCount, List<int>[] adjacency, int[] distanceBuffer, bool[] canHostShop, bool[] canHostShortcut,
            int bestDistance, Assignment result)
        {
            BreadthFirst(result.EntranceRoom, adjacency, distanceBuffer);
            double target = bestDistance / 2.0;
            int shop = -1;
            double shopScore = double.MaxValue;
            for (int i = 0; i < roomCount; i++)
            {
                if (i == result.EntranceRoom || i == result.ExitRoom || canHostShortcut[i]) continue;
                if (!canHostShop[i]) continue;
                if (distanceBuffer[i] < 0) continue;
                double score = Math.Abs(distanceBuffer[i] - target);
                if (score < shopScore)
                {
                    shopScore = score;
                    shop = i;
                }
            }
            // ショップを置ける部屋が階段に取られてしまうことがある（Shop タグの部屋が 1 つしか
            // 出ず、それが階段に選ばれた場合など）。そのまま -1 で返すとショップが消えるので、
            // 階段以外のどこかで妥協する。
            if (shop < 0)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    if (i == result.EntranceRoom || i == result.ExitRoom || canHostShortcut[i]) continue;
                    shop = i;
                    break;
                }
            }

            return shop;
        }

        private static List<int>[] BuildAdjacency(int roomCount, IReadOnlyList<RoomEdge> edges)
        {
            var adjacency = new List<int>[roomCount];
            for (int i = 0; i < roomCount; i++) adjacency[i] = new List<int>();
            foreach (var edge in edges)
            {
                adjacency[edge.RoomA].Add(edge.RoomB);
                adjacency[edge.RoomB].Add(edge.RoomA);
            }
            return adjacency;
        }

        /// <summary>start からの辺数距離を distances に書き込む。到達不能は -1。</summary>
        private static void BreadthFirst(int start, List<int>[] adjacency, int[] distances)
        {
            for (int i = 0; i < distances.Length; i++) distances[i] = -1;
            distances[start] = 0;

            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in adjacency[current])
                {
                    if (distances[next] >= 0) continue;
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }
        }
    }
}
