using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    public enum CellKind
    {
        Empty = 0,
        Room = 1,
    }

    /// <summary>
    /// レイアウトをセル単位の占有グリッドとして見たもの。
    /// どのセル境界に壁を建てるか／どこをドアにするかを、FloorBuilder とデバッグ表示が同じ判定で求められるようにする。
    /// </summary>
    public sealed class FloorOccupancy
    {
        private readonly FloorLayout layout;
        private readonly CellKind[] kinds;
        private readonly int[] roomIndices;
        private readonly Dictionary<(GridPos cell, Direction facing), PlacedSocket> doors
            = new Dictionary<(GridPos, Direction), PlacedSocket>();

        public int Width => layout.Width;
        public int Height => layout.Height;

        public FloorOccupancy(FloorLayout layout)
        {
            this.layout = layout;
            kinds = new CellKind[layout.Width * layout.Height];
            roomIndices = new int[layout.Width * layout.Height];
            for (int i = 0; i < roomIndices.Length; i++) roomIndices[i] = -1;

            foreach (var room in layout.Rooms)
            {
                for (int y = room.Bounds.Y; y <= room.Bounds.MaxY; y++)
                {
                    for (int x = room.Bounds.X; x <= room.Bounds.MaxX; x++)
                    {
                        int index = y * layout.Width + x;
                        kinds[index] = CellKind.Room;
                        roomIndices[index] = room.Index;
                    }
                }

                foreach (var socket in room.UsedSockets()) doors[(socket.Cell, socket.Facing)] = socket;
            }
        }

        public bool InsideGrid(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < layout.Width && p.Y < layout.Height;

        public CellKind KindAt(GridPos p) => InsideGrid(p) ? kinds[p.Y * layout.Width + p.X] : CellKind.Empty;

        public int RoomAt(GridPos p) => InsideGrid(p) ? roomIndices[p.Y * layout.Width + p.X] : -1;

        /// <summary>cell から dir 方向の境界にドアがあるならそのソケット。無ければ null。</summary>
        public PlacedSocket DoorAt(GridPos cell, Direction dir) =>
            doors.TryGetValue((cell, dir), out var socket) ? socket : null;

        /// <summary>
        /// cell から dir 方向のセル境界に何を建てるべきか。
        /// 同じ境界は両側から 2 回問い合わせられるので、重複を避けたい場合は呼び出し側で片側だけ処理すること。
        /// </summary>
        public BoundaryKind BoundaryAt(GridPos cell, Direction dir)
        {
            var here = KindAt(cell);
            if (here == CellKind.Empty) return BoundaryKind.None;

            var neighborCell = cell + dir.Offset();
            var neighbor = KindAt(neighborCell);

            // 同じ部屋の内側には何も建てない。別々の部屋が隣り合う境界は、ドアが無い限り壁になる。
            if (here == CellKind.Room && neighbor == CellKind.Room && RoomAt(cell) == RoomAt(neighborCell))
            {
                return BoundaryKind.None;
            }
            // ドアは両側の部屋にそれぞれ登録されるが、片側しか無い場合に備えて隣も見る。
            if (DoorAt(cell, dir) != null || DoorAt(neighborCell, dir.Opposite()) != null)
            {
                return BoundaryKind.Door;
            }
            return BoundaryKind.Wall;
        }
    }

    public enum BoundaryKind
    {
        None = 0,
        Wall = 1,
        Door = 2,
    }
}
