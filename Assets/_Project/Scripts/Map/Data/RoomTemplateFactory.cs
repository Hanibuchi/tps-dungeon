using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 矩形の部屋に標準的なドア候補を打つヘルパー。
    /// プレースホルダ Prefab の生成とテスト用フィクスチャで同じ配置規則を使うためにここに置いている。
    /// </summary>
    public static class RoomTemplateFactory
    {
        /// <summary>
        /// 外周セル 1 つずつにドア候補を打つ。
        /// 1x1〜2x2 のような小さい部屋では、辺の中央だけに打つと繋ぎ先が足りなくなって成長が止まるため、
        /// 打てる場所には全部打っておいて、使うかどうかは生成時に選ばせる。
        /// 並び順は RoomTemplate.CollectSockets の整列（方向 → x → y）と揃えてある。
        /// </summary>
        public static List<DoorSocketData> CreateDefaultSockets(int width, int height)
        {
            var sockets = new List<DoorSocketData>();

            for (int x = 0; x < width; x++) sockets.Add(new DoorSocketData(new GridPos(x, height - 1), Direction.North));
            for (int y = 0; y < height; y++) sockets.Add(new DoorSocketData(new GridPos(width - 1, y), Direction.East));
            for (int x = 0; x < width; x++) sockets.Add(new DoorSocketData(new GridPos(x, 0), Direction.South));
            for (int y = 0; y < height; y++) sockets.Add(new DoorSocketData(new GridPos(0, y), Direction.West));

            return sockets;
        }
    }
}
