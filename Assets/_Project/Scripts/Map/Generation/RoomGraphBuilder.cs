using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// BSP ツリーの兄弟同士を結んで、全リーフが繋がる木構造のグラフを作る。
    /// さらにループ辺を数本足して袋小路を減らす。
    /// </summary>
    public static class RoomGraphBuilder
    {
        public static List<RoomEdge> Build(
            BspNode root,
            IReadOnlyList<GridRect> leafRects,
            FloorGenerationParams p,
            Random rng)
        {
            var edges = new List<RoomEdge>();
            var connected = new HashSet<long>();

            ConnectSubtree(root, leafRects, edges, connected);
            AddLoopEdges(leafRects, edges, connected, p, rng);

            return edges;
        }

        /// <summary>サブツリーに含まれるリーフ番号を返しつつ、左右の子を 1 本の辺で結ぶ。</summary>
        private static List<int> ConnectSubtree(
            BspNode node,
            IReadOnlyList<GridRect> leafRects,
            List<RoomEdge> edges,
            HashSet<long> connected)
        {
            if (node.IsLeaf) return new List<int> { node.LeafIndex };

            var left = ConnectSubtree(node.Left, leafRects, edges, connected);
            var right = ConnectSubtree(node.Right, leafRects, edges, connected);

            // 左右のリーフ集合から最も近い組を選ぶと、境界をまたぐ短い廊下になる。
            int bestA = left[0];
            int bestB = right[0];
            int bestDistance = int.MaxValue;
            foreach (int a in left)
            {
                foreach (int b in right)
                {
                    int distance = GridRect.Distance(leafRects[a], leafRects[b]);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            AddEdge(edges, connected, bestA, bestB);

            left.AddRange(right);
            return left;
        }

        private static void AddLoopEdges(
            IReadOnlyList<GridRect> leafRects,
            List<RoomEdge> edges,
            HashSet<long> connected,
            FloorGenerationParams p,
            Random rng)
        {
            if (p.ExtraEdgeCount <= 0) return;

            var candidates = new List<(int a, int b, int distance)>();
            for (int a = 0; a < leafRects.Count; a++)
            {
                for (int b = a + 1; b < leafRects.Count; b++)
                {
                    if (connected.Contains(Key(a, b))) continue;
                    int distance = GridRect.Distance(leafRects[a], leafRects[b]);
                    if (distance > p.ExtraEdgeMaxDistance) continue;
                    candidates.Add((a, b, distance));
                }
            }

            // 近い組ほど優先しつつ、同距離の中では毎回違う組が選ばれるようにシャッフルしてから距離順に並べる。
            Shuffle(candidates, rng);
            candidates.Sort((x, y) => x.distance.CompareTo(y.distance));

            int added = 0;
            foreach (var candidate in candidates)
            {
                if (added >= p.ExtraEdgeCount) break;
                if (connected.Contains(Key(candidate.a, candidate.b))) continue;
                AddEdge(edges, connected, candidate.a, candidate.b);
                added++;
            }
        }

        private static void AddEdge(List<RoomEdge> edges, HashSet<long> connected, int a, int b)
        {
            if (a == b) return;
            if (!connected.Add(Key(a, b))) return;
            edges.Add(new RoomEdge(edges.Count, a, b));
        }

        private static long Key(int a, int b)
        {
            int lo = Math.Min(a, b);
            int hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
