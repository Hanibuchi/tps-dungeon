using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// 1 フロアのうちプレイヤーが足を踏み入れた部屋の記録。
    /// ドアを開けるまで中が見えない前提なので、地図には入った部屋だけを描く。
    /// </summary>
    public sealed class FloorExploration
    {
        private readonly FloorLayout layout;
        private readonly bool[] visited;

        public FloorExploration(FloorLayout layout)
        {
            this.layout = layout;
            visited = new bool[layout.Rooms.Count];
        }

        public FloorLayout Layout => layout;

        /// <summary>最後に居た部屋。まだどの部屋にも入っていなければ -1。</summary>
        public int CurrentRoom { get; private set; } = -1;

        public int VisitedCount { get; private set; }

        public bool IsVisited(int room) => room >= 0 && room < visited.Length && visited[room];

        /// <summary>
        /// cell に居ることを記録する。部屋の外（ドアの境界上や壁の外）なら何もせず、直前の部屋を保つ。
        /// 初めて入った部屋なら true を返す。
        /// </summary>
        public bool Visit(GridPos cell)
        {
            int room = RoomAt(cell);
            if (room < 0) return false;

            CurrentRoom = room;
            if (visited[room]) return false;

            visited[room] = true;
            VisitedCount++;
            return true;
        }

        /// <summary>cell を含む部屋の番号。無ければ -1。部屋は高々数十なので総当たりで足りる。</summary>
        public int RoomAt(GridPos cell)
        {
            IReadOnlyList<RoomInstance> rooms = layout.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Bounds.Contains(cell)) return i;
            }
            return -1;
        }
    }
}
