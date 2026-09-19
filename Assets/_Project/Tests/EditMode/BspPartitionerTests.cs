using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    public sealed class BspPartitionerTests
    {
        [Test]
        public void Leaves_TileTheFloorWithoutOverlap()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(50))
            {
                var root = BspPartitioner.Partition(parameters, new System.Random(seed));
                var leaves = BspPartitioner.CollectLeafRects(root);

                int coveredArea = 0;
                foreach (var leaf in leaves) coveredArea += leaf.Area;
                Assert.AreEqual(parameters.Width * parameters.Height, coveredArea,
                    $"seed {seed}: リーフの面積合計がフロア全体と一致しない");

                for (int i = 0; i < leaves.Count; i++)
                {
                    for (int j = i + 1; j < leaves.Count; j++)
                    {
                        Assert.IsFalse(leaves[i].Overlaps(leaves[j]),
                            $"seed {seed}: リーフ {leaves[i]} と {leaves[j]} が重なっている");
                    }
                }
            }
        }

        [Test]
        public void Leaves_AreNeverSmallerThanMinLeafSize()
        {
            var parameters = FloorGenerationFixture.Params();

            foreach (int seed in FloorGenerationFixture.Seeds(50))
            {
                var root = BspPartitioner.Partition(parameters, new System.Random(seed));
                foreach (var leaf in BspPartitioner.CollectLeafRects(root))
                {
                    Assert.GreaterOrEqual(leaf.Width, parameters.MinLeafSize, $"seed {seed}: {leaf} が細すぎる");
                    Assert.GreaterOrEqual(leaf.Height, parameters.MinLeafSize, $"seed {seed}: {leaf} が薄すぎる");
                }
            }
        }

        [Test]
        public void LeafIndices_AreSequential()
        {
            var root = BspPartitioner.Partition(FloorGenerationFixture.Params(), new System.Random(1));
            var seen = new List<int>();
            Collect(root, seen);

            seen.Sort();
            for (int i = 0; i < seen.Count; i++) Assert.AreEqual(i, seen[i]);
        }

        private static void Collect(BspNode node, List<int> into)
        {
            if (node.IsLeaf)
            {
                into.Add(node.LeafIndex);
                return;
            }
            Collect(node.Left, into);
            Collect(node.Right, into);
        }
    }
}
