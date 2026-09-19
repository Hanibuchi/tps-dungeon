using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 矩形の部屋に標準的なドア候補を打つヘルパー。
    /// プレースホルダ Prefab の生成とテスト用フィクスチャで同じ配置規則を使うためにここに置いている。
    /// </summary>
    public static class RoomTemplateFactory
    {
        /// <summary>この長さを超える辺には 2 箇所ドア候補を置く。</summary>
        private const int TwoSocketEdgeLength = 8;

        /// <summary>四辺すべてにドア候補を打つ。長い辺は 2 箇所。</summary>
        public static List<DoorSocketData> CreateDefaultSockets(int width, int height)
        {
            var sockets = new List<DoorSocketData>();

            foreach (int x in EdgePositions(width)) sockets.Add(new DoorSocketData(new GridPos(x, height - 1), Direction.North));
            foreach (int y in EdgePositions(height)) sockets.Add(new DoorSocketData(new GridPos(width - 1, y), Direction.East));
            foreach (int x in EdgePositions(width)) sockets.Add(new DoorSocketData(new GridPos(x, 0), Direction.South));
            foreach (int y in EdgePositions(height)) sockets.Add(new DoorSocketData(new GridPos(0, y), Direction.West));

            return sockets;
        }

        /// <summary>長さ length の辺に沿ったドア候補の位置。角は避ける。</summary>
        private static IEnumerable<int> EdgePositions(int length)
        {
            if (length <= 2)
            {
                yield return length / 2;
                yield break;
            }
            if (length < TwoSocketEdgeLength)
            {
                yield return length / 2;
                yield break;
            }
            yield return length / 3;
            yield return length * 2 / 3;
        }
    }
}
