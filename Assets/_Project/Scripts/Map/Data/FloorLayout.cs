using System.Collections.Generic;
using System.Text;

namespace TpsDungeon.Map.Data
{
    /// <summary>部屋 2 つを結ぶ 1 本の廊下。</summary>
    public sealed class RoomEdge
    {
        public int Index { get; }
        public int RoomA { get; }
        public int RoomB { get; }

        /// <summary>A 側 / B 側で実際に使われたソケット。カーブ前は null。</summary>
        public PlacedSocket SocketA { get; internal set; }
        public PlacedSocket SocketB { get; internal set; }

        /// <summary>2 つのドアの間を埋める廊下セル列。ドアセル自体は含まない（隣接する部屋同士なら空になる）。</summary>
        public List<GridPos> Path { get; } = new List<GridPos>();

        /// <summary>廊下を通せたか。通せなかった辺は具現化もされない。</summary>
        public bool IsCarved { get; internal set; }

        public RoomEdge(int index, int roomA, int roomB)
        {
            Index = index;
            RoomA = roomA;
            RoomB = roomB;
        }

        public int Other(int room) => room == RoomA ? RoomB : RoomA;

        public override string ToString() => $"Edge{Index}({RoomA}<->{RoomB}{(IsCarved ? $" len {Path.Count}" : " uncarved")})";
    }

    /// <summary>1 フロア分の生成結果。Unity には一切依存しない。</summary>
    public sealed class FloorLayout
    {
        public int Seed { get; }
        public int Width { get; }
        public int Height { get; }

        public IReadOnlyList<RoomInstance> Rooms { get; }
        public IReadOnlyList<RoomEdge> Edges { get; }

        /// <summary>廊下が占めるセルの集合。部屋の内部セルは含まない。</summary>
        public IReadOnlyCollection<GridPos> CorridorCells => CorridorCellSet;

        internal HashSet<GridPos> CorridorCellSet { get; }

        /// <summary>BSP のリーフ矩形（デバッグ表示用）。</summary>
        public IReadOnlyList<GridRect> LeafRects { get; }

        public int StairUpRoom { get; internal set; } = -1;
        public int StairDownRoom { get; internal set; } = -1;
        public int ShopRoom { get; internal set; } = -1;

        public FloorLayout(
            int seed,
            int width,
            int height,
            IReadOnlyList<RoomInstance> rooms,
            IReadOnlyList<RoomEdge> edges,
            HashSet<GridPos> corridorCells,
            IReadOnlyList<GridRect> leafRects)
        {
            Seed = seed;
            Width = width;
            Height = height;
            Rooms = rooms;
            Edges = edges;
            CorridorCellSet = corridorCells;
            LeafRects = leafRects;
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
                sb.Append(edge.RoomA).Append('-').Append(edge.RoomB).Append(':');
                foreach (var cell in edge.Path) sb.Append(cell);
                sb.Append(';');
            }
            sb.Append("|up=").Append(StairUpRoom).Append(",down=").Append(StairDownRoom).Append(",shop=").Append(ShopRoom);
            return sb.ToString();
        }
    }
}
