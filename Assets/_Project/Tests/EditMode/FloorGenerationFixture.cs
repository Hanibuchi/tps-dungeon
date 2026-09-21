using System.Collections.Generic;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>テスト用の部屋テンプレートと生成パラメータ。第1層の実際のラインナップに合わせてある。</summary>
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
            MinRoomCount = 10,
            MaxRoomCount = 20,
            MaxPackAttempts = 6,
            StartJitter = 4,
            ExtraDoorCount = 2,
            // Settings/Map/L1_RoomCatalog の登録内容と揃えてある。実際に出荷している構成を検証したいので、
            // ここを変えるときはプレースホルダ Prefab 側（PlaceholderMapAssetGenerator.Rooms）も合わせること。
            Templates = new List<RoomTemplateData>
            {
                Template("Room_1x1", 1, 1, RoomTag.Normal, true, 1.0f),
                Template("Room_1x2", 1, 2, RoomTag.Normal, true, 0.8f),
                Template("Room_2x2", 2, 2, RoomTag.Normal, true, 0.5f),
                Template("Room_Shop", 1, 1, RoomTag.Shop, true, 0.2f),
                Template("Room_Stair", 1, 1, RoomTag.Stair, true, 0.2f),
            },
        };

        /// <summary>まとまった数のシードを流して統計的な破綻を拾うためのシード列。</summary>
        public static IEnumerable<int> Seeds(int count)
        {
            for (int seed = 1; seed <= count; seed++) yield return seed;
        }
    }
}
