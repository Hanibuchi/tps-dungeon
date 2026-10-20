using System.Collections.Generic;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Tests
{
    /// <summary>テスト用の部屋テンプレートと生成パラメータ。第1層の実際のラインナップに合わせてある。</summary>
    internal static class FloorGenerationFixture
    {
        public static RoomTemplateData Template(
            string id, int width, int height, RoomTag tags, bool allowRotation, float weight = 1f, int maxDoors = 0) =>
            new RoomTemplateData(id, width, height, tags, allowRotation, weight,
                RoomTemplateFactory.CreateDefaultSockets(width, height), maxDoors);

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
                // 入口・出口の部屋はドアを南の 1 面だけにしてある。
                new RoomTemplateData("Room_Stair", 1, 1, RoomTag.Stair, true, 0.2f,
                    RoomTemplateFactory.CreateSocketsOnSide(1, 1, Direction.South)),
            },
        };

        /// <summary>
        /// ボス階の設計図の部屋のテンプレート（BossFloorLayout.TemplateId の ID から）。階段は南の辺、ショートカットの部屋は西の辺だけにドア候補、
        /// ほかは大きさ（ID の末尾の 幅x奥行）どおりで外周全部。
        /// </summary>
        public static RoomTemplateData BossTemplate(string id)
        {
            switch (id)
            {
                case BossFloorLayout.StairId:
                    return new RoomTemplateData(id, 1, 1, RoomTag.Stair, true, 0.2f, RoomTemplateFactory.CreateSocketsOnSide(1, 1, Direction.South));
                case BossFloorLayout.ShortcutId:
                    return new RoomTemplateData(id, 1, 1, RoomTag.Boss, false, 0f, RoomTemplateFactory.CreateSocketsOnSide(1, 1, Direction.West));
                case BossFloorLayout.ArenaId:
                    return Template(id, 5, 5, RoomTag.Boss, false, 0f);
            }
            var size = id.Substring(id.LastIndexOf('_') + 1).Split('x');
            var tag = id.StartsWith(BossFloorLayout.HallPrefix) ? RoomTag.Boss : RoomTag.Normal;
            return Template(id, int.Parse(size[0]), int.Parse(size[1]), tag, false, 0f);
        }

        /// <summary>設計図の本文からボス階を組む（テンプレートは <see cref="BossTemplate"/>）。</summary>
        public static FloorLayout BossLayout(string body, int seed = 1, int size = 16)
        {
            var errors = new List<string>();
            var blueprint = BossFloorBlueprint.Parse(body, errors);
            if (errors.Count > 0) throw new System.ArgumentException(string.Join(" / ", errors));
            return BossFloorLayout.Generate(blueprint, BossTemplate, size, size, seed);
        }

        /// <summary>今までの 1 層の形（入口 → 3x1 → ボス部屋 → 出口、3x1 の東にショートカットの部屋）。</summary>
        public const string SimpleBossBlueprint =
            "...X...\n" +
            ".AAAAA.\n" +
            ".AAAAA.\n" +
            ".AAAAA.\n" +
            ".AAAAA.\n" +
            ".AAAAA.\n" +
            "..HHHS.\n" +
            "...E...\n" +
            "扉: E-H\n" +
            "敵: H\n";

        /// <summary>まとまった数のシードを流して統計的な破綻を拾うためのシード列。</summary>
        public static IEnumerable<int> Seeds(int count)
        {
            for (int seed = 1; seed <= count; seed++) yield return seed;
        }
    }
}
