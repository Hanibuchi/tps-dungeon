using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>1 フロアを生成するための入力パラメータ。FloorConfig(ScriptableObject) から組み立てる。</summary>
    public sealed class FloorGenerationParams
    {
        /// <summary>フロアのセル数（X 方向）。</summary>
        public int Width = 64;

        /// <summary>フロアのセル数（Y 方向 = ワールド Z）。</summary>
        public int Height = 64;

        /// <summary>BSP のリーフがこれ未満にならないようにする。最大の部屋テンプレート + 余白より大きくすること。</summary>
        public int MinLeafSize = 14;

        /// <summary>BSP の分割の深さ上限。最大リーフ数は 2^MaxDepth。</summary>
        public int MaxDepth = 4;

        /// <summary>分割位置の比率レンジ。0.5 に近いほど均等な部屋割りになる。</summary>
        public float SplitRatioMin = 0.35f;
        public float SplitRatioMax = 0.65f;

        /// <summary>リーフ矩形の内側にとる余白セル数。部屋同士の間に廊下を通す隙間になる。</summary>
        public int LeafPadding = 1;

        /// <summary>木構造の必須辺に加えて張るループ用の辺の本数。袋小路を減らす。</summary>
        public int ExtraEdgeCount = 2;

        /// <summary>ループ辺を張る候補とみなす部屋同士の最大距離（セル）。</summary>
        public int ExtraEdgeMaxDistance = 24;

        /// <summary>廊下探索で曲がるときに加算されるコスト。大きいほど直線的な廊下になる。</summary>
        public int CorridorTurnPenalty = 4;

        /// <summary>1 本の辺につき試すソケットの組み合わせ数の上限。</summary>
        public int MaxSocketPairAttempts = 12;

        /// <summary>ショップ部屋を配置するか。</summary>
        public bool IncludeShop = true;

        /// <summary>抽選対象の部屋テンプレート。</summary>
        public IReadOnlyList<RoomTemplateData> Templates = Array.Empty<RoomTemplateData>();

        public void Validate()
        {
            if (Width < MinLeafSize || Height < MinLeafSize)
                throw new ArgumentException($"フロアサイズ {Width}x{Height} が MinLeafSize {MinLeafSize} より小さい");
            if (MinLeafSize < 3)
                throw new ArgumentException($"MinLeafSize が小さすぎる: {MinLeafSize}");
            if (MaxDepth < 1)
                throw new ArgumentException($"MaxDepth が小さすぎる: {MaxDepth}");
            if (SplitRatioMin <= 0f || SplitRatioMax >= 1f || SplitRatioMin > SplitRatioMax)
                throw new ArgumentException($"分割比が不正: {SplitRatioMin}..{SplitRatioMax}");
            if (LeafPadding < 0)
                throw new ArgumentException($"LeafPadding が負: {LeafPadding}");
            if (Templates == null || Templates.Count == 0)
                throw new ArgumentException("部屋テンプレートが 1 つも渡されていない");
        }

        public FloorGenerationParams Clone() => (FloorGenerationParams)MemberwiseClone();
    }
}
