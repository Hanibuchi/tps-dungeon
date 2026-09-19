using UnityEngine;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 1 つの階の生成設定。階ごとに 1 つ作り、その階のシーンの FloorBootstrap から参照する。
    /// 上の層ほど太くする、といった層ごとの差はこのアセットの値で表現する。
    /// </summary>
    [CreateAssetMenu(fileName = "FloorConfig", menuName = "TPS Dungeon/Floor Config")]
    public sealed class FloorConfig : ScriptableObject
    {
        [Header("グリッド")]
        [Min(8)] public int widthInCells = 64;
        [Min(8)] public int heightInCells = 64;
        [Min(0.5f), Tooltip("1 セルの一辺（メートル）。部屋テンプレートの cellSize と揃えること。")]
        public float cellSize = 3f;

        [Header("BSP")]
        [Min(4), Tooltip("リーフの最小サイズ。最大の部屋テンプレート + 余白×2 より大きくすること。")]
        public int minLeafSize = 14;
        [Min(1), Tooltip("分割の深さ上限。最大リーフ数は 2 の maxDepth 乗。")]
        public int maxDepth = 4;
        [Range(0.05f, 0.5f)] public float splitRatioMin = 0.35f;
        [Range(0.5f, 0.95f)] public float splitRatioMax = 0.65f;
        [Min(0), Tooltip("リーフの内側にとる余白。部屋同士の間に廊下を通す隙間になる。")]
        public int leafPadding = 1;

        [Header("接続")]
        [Min(0), Tooltip("木構造の必須辺に加えて張るループ用の辺の本数。")]
        public int extraEdgeCount = 2;
        [Min(1)] public int extraEdgeMaxDistance = 24;
        [Min(0), Tooltip("廊下が曲がるときのコスト。大きいほど直線的になる。")]
        public int corridorTurnPenalty = 4;

        [Header("部屋")]
        public RoomTemplateCatalog roomCatalog;
        public bool includeShop = true;

        [Header("共通パーツ")]
        [Tooltip("セル 1 つ分の壁。ローカル原点が壁の中心で、+Z 側が部屋の外を向く。")]
        public GameObject wallPrefab;
        [Tooltip("セル 1 つ分のドア。向きは壁と同じ規約。")]
        public GameObject doorPrefab;
        [Tooltip("廊下の床タイル。セル 1 つ分。")]
        public GameObject corridorFloorPrefab;
        public GameObject stairUpPrefab;
        public GameObject stairDownPrefab;
        public GameObject shopMarkerPrefab;

        public FloorGenerationParams BuildParams()
        {
            var parameters = new FloorGenerationParams
            {
                Width = widthInCells,
                Height = heightInCells,
                MinLeafSize = minLeafSize,
                MaxDepth = maxDepth,
                SplitRatioMin = splitRatioMin,
                SplitRatioMax = splitRatioMax,
                LeafPadding = leafPadding,
                ExtraEdgeCount = extraEdgeCount,
                ExtraEdgeMaxDistance = extraEdgeMaxDistance,
                CorridorTurnPenalty = corridorTurnPenalty,
                IncludeShop = includeShop,
                Templates = roomCatalog != null
                    ? roomCatalog.BuildTemplateData()
                    : new System.Collections.Generic.List<Data.RoomTemplateData>(),
            };
            return parameters;
        }
    }
}
