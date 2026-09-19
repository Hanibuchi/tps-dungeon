using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>BSP ツリーの 1 ノード。</summary>
    public sealed class BspNode
    {
        public GridRect Rect { get; }
        public BspNode Left { get; internal set; }
        public BspNode Right { get; internal set; }

        /// <summary>リーフに振られた通し番号。内部ノードは -1。</summary>
        public int LeafIndex { get; internal set; } = -1;

        public bool IsLeaf => Left == null && Right == null;

        public BspNode(GridRect rect)
        {
            Rect = rect;
        }
    }

    /// <summary>フロア矩形を再帰的に分割して部屋の枠（リーフ）を作る。</summary>
    public static class BspPartitioner
    {
        public static BspNode Partition(FloorGenerationParams p, Random rng)
        {
            var root = new BspNode(new GridRect(0, 0, p.Width, p.Height));
            Split(root, 0, p, rng);

            int nextLeafIndex = 0;
            AssignLeafIndices(root, ref nextLeafIndex);
            return root;
        }

        public static List<GridRect> CollectLeafRects(BspNode root)
        {
            var result = new List<GridRect>();
            CollectLeaves(root, result);
            return result;
        }

        private static void CollectLeaves(BspNode node, List<GridRect> into)
        {
            if (node.IsLeaf)
            {
                into.Add(node.Rect);
                return;
            }
            CollectLeaves(node.Left, into);
            CollectLeaves(node.Right, into);
        }

        private static void AssignLeafIndices(BspNode node, ref int next)
        {
            if (node.IsLeaf)
            {
                node.LeafIndex = next++;
                return;
            }
            AssignLeafIndices(node.Left, ref next);
            AssignLeafIndices(node.Right, ref next);
        }

        private static void Split(BspNode node, int depth, FloorGenerationParams p, Random rng)
        {
            if (depth >= p.MaxDepth) return;

            var rect = node.Rect;
            bool canSplitVertically = rect.Width >= p.MinLeafSize * 2;   // X 軸で割る
            bool canSplitHorizontally = rect.Height >= p.MinLeafSize * 2; // Y 軸で割る
            if (!canSplitVertically && !canSplitHorizontally) return;

            bool vertical;
            if (canSplitVertically && canSplitHorizontally)
            {
                // 細長い矩形は長辺で割り、正方形に近いときだけランダムに決める。
                if (rect.Width > rect.Height * 5 / 4) vertical = true;
                else if (rect.Height > rect.Width * 5 / 4) vertical = false;
                else vertical = rng.Next(2) == 0;
            }
            else
            {
                vertical = canSplitVertically;
            }

            int length = vertical ? rect.Width : rect.Height;
            double ratio = p.SplitRatioMin + rng.NextDouble() * (p.SplitRatioMax - p.SplitRatioMin);
            int cut = (int)Math.Round(length * ratio);
            cut = Math.Max(p.MinLeafSize, Math.Min(length - p.MinLeafSize, cut));

            if (vertical)
            {
                node.Left = new BspNode(new GridRect(rect.X, rect.Y, cut, rect.Height));
                node.Right = new BspNode(new GridRect(rect.X + cut, rect.Y, rect.Width - cut, rect.Height));
            }
            else
            {
                node.Left = new BspNode(new GridRect(rect.X, rect.Y, rect.Width, cut));
                node.Right = new BspNode(new GridRect(rect.X, rect.Y + cut, rect.Width, rect.Height - cut));
            }

            Split(node.Left, depth + 1, p, rng);
            Split(node.Right, depth + 1, p, rng);
        }
    }
}
