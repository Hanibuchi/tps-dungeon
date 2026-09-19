using System.Collections.Generic;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>テスト用の部屋テンプレートと生成パラメータ。第1層の想定ラインナップに合わせてある。</summary>
    internal static class FloorGenerationFixture
    {
        public static RoomTemplateData Template(
            string id, int width, int height, RoomTag tags, bool allowRotation, float weight = 1f) =>
            new RoomTemplateData(id, width, height, tags, allowRotation, weight,
                RoomTemplateFactory.CreateDefaultSockets(width, height));

        public static FloorGenerationParams Params() => new FloorGenerationParams
        {
            Width = 64,
            Height = 64,
            MinLeafSize = 14,
            MaxDepth = 4,
            Templates = new List<RoomTemplateData>
            {
                Template("Hall_6x6", 6, 6, RoomTag.Normal, true, 1.0f),
                Template("Guard_8x6", 8, 6, RoomTag.Normal, true, 1.0f),
                Template("Chapel_10x8", 10, 8, RoomTag.Normal, true, 0.8f),
                Template("Great_12x10", 12, 10, RoomTag.Normal, true, 0.5f),
                Template("Shop_8x8", 8, 8, RoomTag.Shop, false, 1.0f),
                Template("Stair_6x6", 6, 6, RoomTag.Stair, true, 1.0f),
            },
        };

        /// <summary>まとまった数のシードを流して統計的な破綻を拾うためのシード列。</summary>
        public static IEnumerable<int> Seeds(int count)
        {
            for (int seed = 1; seed <= count; seed++) yield return seed;
        }
    }
}
