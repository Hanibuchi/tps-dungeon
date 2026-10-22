using System;
using System.Collections.Generic;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// 魂の強化の木を、根を真ん中にしてマインドマップのように放射状に並べる。UnityEngine に依存しない。
    /// - 根の子は全周を、その先の葉の数に比例して分け合う。孫から先は、親の扇をまた葉の数で分ける。
    /// - 置く場所は扇の真ん中の向きで、根からの距離は 1 段目が <see cref="FirstRing"/>、そこから 1 段ごとに <see cref="RingStep"/>。
    /// 並べるのは見えない強化も含めた全部なので、解放して先が見えてきても、もう出ている強化は動かない。
    /// 座標は画面の向き（x は右、y は下）。根は (0, 0)。
    /// </summary>
    public static class RadialTreeLayout
    {
        public const float FirstRing = 240f;
        public const float RingStep = 125f;

        public readonly struct Point
        {
            public readonly float X;
            public readonly float Y;
            /// <summary>根から見た向き（ラジアン。0 が右、時計回り）。</summary>
            public readonly float Angle;

            public Point(float x, float y, float angle)
            {
                X = x;
                Y = y;
                Angle = angle;
            }
        }

        /// <param name="startAngle">根の最初の子を置き始める向き（ラジアン。既定は真上から）。</param>
        public static Dictionary<string, Point> Layout(SoulUpgradeTree tree, float firstRing = FirstRing, float ringStep = RingStep,
            float startAngle = (float)(-Math.PI / 2))
        {
            var points = new Dictionary<string, Point>(StringComparer.Ordinal);
            if (tree == null || tree.Root == null) return points;

            var leaves = new Dictionary<string, int>(StringComparer.Ordinal);
            CountLeaves(tree, tree.Root, leaves);

            points[tree.Root] = new Point(0f, 0f, 0f);

            // 根の子は全周を分ける。最初の子の扇の真ん中が startAngle に来るように回しておく。
            IReadOnlyList<string> top = tree.Children(tree.Root);
            int total = leaves[tree.Root];
            double full = Math.PI * 2;
            double first = top.Count > 0 ? full * leaves[top[0]] / total : 0;
            Place(tree, tree.Root, startAngle - first / 2, startAngle - first / 2 + full, 0, firstRing, ringStep, leaves, points);
            return points;
        }

        private static int CountLeaves(SoulUpgradeTree tree, string id, Dictionary<string, int> leaves)
        {
            IReadOnlyList<string> kids = tree.Children(id);
            int count = 0;
            foreach (string kid in kids) count += CountLeaves(tree, kid, leaves);
            if (count == 0) count = 1;
            leaves[id] = count;
            return count;
        }

        private static void Place(SoulUpgradeTree tree, string id, double from, double to, int depth, float firstRing, float ringStep,
            Dictionary<string, int> leaves, Dictionary<string, Point> points)
        {
            IReadOnlyList<string> kids = tree.Children(id);
            if (kids.Count == 0) return;

            int total = 0;
            foreach (string kid in kids) total += leaves[kid];

            double at = from;
            float radius = firstRing + ringStep * depth;
            foreach (string kid in kids)
            {
                double span = (to - from) * leaves[kid] / total;
                double angle = at + span / 2;
                points[kid] = new Point((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius), (float)angle);
                Place(tree, kid, at, at + span, depth + 1, firstRing, ringStep, leaves, points);
                at += span;
            }
        }
    }
}
