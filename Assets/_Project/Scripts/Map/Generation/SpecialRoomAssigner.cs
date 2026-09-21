using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>上り階段・下り階段・ショップをどの部屋に置くかを決める。</summary>
    public static class SpecialRoomAssigner
    {
        public sealed class Assignment
        {
            public int StairUpRoom = -1;
            public int StairDownRoom = -1;
            public int ShopRoom = -1;
        }

        /// <param name="canHostStair">その部屋が階段を置けるテンプレートか。</param>
        /// <param name="canHostShop">その部屋がショップを置けるテンプレートか。</param>
        public static Assignment Assign(
            int roomCount,
            IReadOnlyList<RoomEdge> edges,
            bool[] canHostStair,
            bool[] canHostShop,
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
                result.StairUpRoom = bestA;
                result.StairDownRoom = bestB;
            }
            else
            {
                result.StairUpRoom = bestB;
                result.StairDownRoom = bestA;
            }

            if (!p.IncludeShop) return result;

            // ショップは入口と出口のちょうど中間あたりに置く。
            BreadthFirst(result.StairUpRoom, adjacency, distanceBuffer);
            double target = bestDistance / 2.0;
            int shop = -1;
            double shopScore = double.MaxValue;
            for (int i = 0; i < roomCount; i++)
            {
                if (i == result.StairUpRoom || i == result.StairDownRoom) continue;
                if (!canHostShop[i]) continue;
                if (distanceBuffer[i] < 0) continue;
                double score = Math.Abs(distanceBuffer[i] - target);
                if (score < shopScore)
                {
                    shopScore = score;
                    shop = i;
                }
            }
            result.ShopRoom = shop;

            return result;
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
