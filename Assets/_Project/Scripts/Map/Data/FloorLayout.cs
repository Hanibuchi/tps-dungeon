using System.Collections.Generic;
using System.Text;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 辺を共有して隣り合う部屋 2 つを結ぶ 1 枚のドア。
    /// 廊下は無いので、辺そのものが「この壁にドアが開いている」という事実を表す。
    /// </summary>
    public sealed class RoomEdge
    {
        public int Index { get; }
        public int RoomA { get; }
        public int RoomB { get; }

        /// <summary>A 側 / B 側で向かい合っているソケット。2 つは必ず隣接セルで逆向き。</summary>
        public PlacedSocket SocketA { get; internal set; }
        public PlacedSocket SocketB { get; internal set; }

        /// <summary>普通のドアの代わりに大扉（両開き）を置く辺か。ボス部屋の入口など。</summary>
        public bool IsGrandDoor { get; internal set; }

        /// <summary>閉ざされた扉を置く辺か。ボスを倒すまで開かない出口の部屋の扉など。</summary>
        public bool IsLocked { get; internal set; }

        public RoomEdge(int index, int roomA, int roomB)
        {
            Index = index;
            RoomA = roomA;
            RoomB = roomB;
        }

        public int Other(int room) => room == RoomA ? RoomB : RoomA;

        public override string ToString() => $"Edge{Index}({RoomA}<->{RoomB})";
    }

    /// <summary>1 フロア分の生成結果。Unity には一切依存しない。</summary>
    public sealed class FloorLayout
    {
        public int Seed { get; }
        public int Width { get; }
        public int Height { get; }

        public IReadOnlyList<RoomInstance> Rooms { get; }
        public IReadOnlyList<RoomEdge> Edges { get; }

        /// <summary>
        /// 実際に部屋が置かれている範囲の外接矩形。
        /// 部屋はグリッド全体には広がらないので、俯瞰カメラの画角合わせにはこちらを使う。
        /// </summary>
        public GridRect RoomsBounds { get; }

        public int EntranceRoom { get; internal set; } = -1;
        public int ExitRoom { get; internal set; } = -1;
        public int ShopRoom { get; internal set; } = -1;

        /// <summary>ショートカット解放用の場所（目印を置くセル）。無ければ null。</summary>
        public GridPos? ShortcutCell { get; internal set; }

        /// <summary>
        /// 間取りが左右対称なら、その軸になる列（セル x が 2 * MirrorColumn - x に映る）。窓も左右で揃える。
        /// ランダムに作る階は null。
        /// </summary>
        public int? MirrorColumn { get; internal set; }

        public FloorLayout(
            int seed,
            int width,
            int height,
            IReadOnlyList<RoomInstance> rooms,
            IReadOnlyList<RoomEdge> edges)
        {
            Seed = seed;
            Width = width;
            Height = height;
            Rooms = rooms;
            Edges = edges;
            RoomsBounds = ComputeRoomsBounds(rooms, width, height);
        }

        public RoomInstance RoomAt(int index) => index >= 0 && index < Rooms.Count ? Rooms[index] : null;

        public bool IsInsideGrid(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        /// <summary>
        /// レイアウト全体を表す文字列。同じシード・同じパラメータなら必ず一致するので、
        /// 決定性テストの比較キーとして使う。
        /// </summary>
        public string ComputeSignature()
        {
            var sb = new StringBuilder();
            sb.Append(Width).Append('x').Append(Height).Append('|');
            foreach (var room in Rooms)
            {
                sb.Append(room.Template.Id).Append(':').Append(room.Rotation).Append(':')
                  .Append(room.Bounds).Append(':').Append((int)room.Role).Append(';');
            }
            sb.Append('|');
            foreach (var edge in Edges)
            {
                // B 側は A 側から一意に決まる（隣接セルで逆向き）ので A 側だけ出せば足りる。
                sb.Append(edge.RoomA).Append('-').Append(edge.RoomB).Append('@')
                  .Append(edge.SocketA.Cell).Append('>').Append((int)edge.SocketA.Facing)
                  .Append(edge.IsGrandDoor ? "!" : "").Append(edge.IsLocked ? "#" : "").Append(';');
            }
            sb.Append("|up=").Append(EntranceRoom).Append(",down=").Append(ExitRoom).Append(",shop=").Append(ShopRoom);
            if (ShortcutCell.HasValue) sb.Append(",shortcut=").Append(ShortcutCell.Value);
            if (MirrorColumn.HasValue) sb.Append(",mirror=").Append(MirrorColumn.Value);
            return sb.ToString();
        }

        private static GridRect ComputeRoomsBounds(IReadOnlyList<RoomInstance> rooms, int width, int height)
        {
            if (rooms == null || rooms.Count == 0) return new GridRect(0, 0, width, height);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var room in rooms)
            {
                if (room.Bounds.X < minX) minX = room.Bounds.X;
                if (room.Bounds.Y < minY) minY = room.Bounds.Y;
                if (room.Bounds.MaxX > maxX) maxX = room.Bounds.MaxX;
                if (room.Bounds.MaxY > maxY) maxY = room.Bounds.MaxY;
            }
            return new GridRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
