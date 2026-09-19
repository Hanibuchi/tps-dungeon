using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>
    /// 部屋グラフの各辺について、両端の部屋のドアソケットを 1 つずつ選び、
    /// 部屋の内部を避けながら廊下を通す。曲がるとコストが増える A* なので、なるべく直線的になる。
    /// </summary>
    public sealed class CorridorCarver
    {
        private const int Blocked = -1;

        private readonly int width;
        private readonly int height;
        private readonly FloorGenerationParams parameters;

        /// <summary>各セルを占有している部屋の番号。空きセルは -1。</summary>
        private readonly int[] roomGrid;

        private readonly int[] gScore;
        private readonly int[] cameFrom;
        private readonly MinHeap open = new MinHeap();

        public CorridorCarver(int width, int height, IReadOnlyList<RoomInstance> rooms, FloorGenerationParams parameters)
        {
            this.width = width;
            this.height = height;
            this.parameters = parameters;

            roomGrid = new int[width * height];
            for (int i = 0; i < roomGrid.Length; i++) roomGrid[i] = Blocked;

            foreach (var room in rooms)
            {
                for (int y = room.Bounds.Y; y <= room.Bounds.MaxY; y++)
                {
                    for (int x = room.Bounds.X; x <= room.Bounds.MaxX; x++)
                    {
                        roomGrid[y * width + x] = room.Index;
                    }
                }
            }

            gScore = new int[width * height * 4];
            cameFrom = new int[width * height * 4];
        }

        /// <summary>全辺に廊下を通し、廊下が占めるセルの集合を返す。</summary>
        public HashSet<GridPos> CarveAll(IReadOnlyList<RoomEdge> edges, IReadOnlyList<RoomInstance> rooms)
        {
            var corridorCells = new HashSet<GridPos>();

            foreach (var edge in edges)
            {
                var roomA = rooms[edge.RoomA];
                var roomB = rooms[edge.RoomB];

                foreach (var (socketA, socketB) in RankSocketPairs(roomA, roomB))
                {
                    var path = FindPath(socketA, socketB);
                    if (path == null) continue;

                    socketA.UsedByEdge = edge.Index;
                    socketB.UsedByEdge = edge.Index;
                    edge.SocketA = socketA;
                    edge.SocketB = socketB;
                    edge.IsCarved = true;

                    // path の両端はドアセル（部屋の一部）なので、廊下セルからは除く。
                    for (int i = 1; i < path.Count - 1; i++)
                    {
                        edge.Path.Add(path[i]);
                        corridorCells.Add(path[i]);
                    }
                    break;
                }
            }

            return corridorCells;
        }

        /// <summary>試すソケットの組み合わせを、望ましい順に並べて返す。</summary>
        private IEnumerable<(PlacedSocket a, PlacedSocket b)> RankSocketPairs(RoomInstance roomA, RoomInstance roomB)
        {
            var centerA = roomA.Bounds.Center;
            var centerB = roomB.Bounds.Center;
            int dx = centerB.X - centerA.X;
            int dy = centerB.Y - centerA.Y;

            Direction primary;
            if (Math.Abs(dx) >= Math.Abs(dy)) primary = dx >= 0 ? Direction.East : Direction.West;
            else primary = dy >= 0 ? Direction.North : Direction.South;

            // 相手の方を向いたソケット同士を最優先し、次に距離が近い順。
            const int facingPenalty = 1000;
            var ranked = new List<(int score, int tieA, int tieB, PlacedSocket a, PlacedSocket b)>();

            for (int i = 0; i < roomA.Sockets.Count; i++)
            {
                var socketA = roomA.Sockets[i];
                if (socketA.IsUsed) continue;
                for (int j = 0; j < roomB.Sockets.Count; j++)
                {
                    var socketB = roomB.Sockets[j];
                    if (socketB.IsUsed) continue;

                    int score = GridPos.Manhattan(socketA.ExitCell, socketB.ExitCell);
                    if (socketA.Facing != primary) score += facingPenalty;
                    if (socketB.Facing != primary.Opposite()) score += facingPenalty;
                    ranked.Add((score, i, j, socketA, socketB));
                }
            }

            ranked.Sort((x, y) =>
            {
                int byScore = x.score.CompareTo(y.score);
                if (byScore != 0) return byScore;
                int byA = x.tieA.CompareTo(y.tieA);
                return byA != 0 ? byA : x.tieB.CompareTo(y.tieB);
            });

            int attempts = Math.Min(ranked.Count, Math.Max(1, parameters.MaxSocketPairAttempts));
            for (int i = 0; i < attempts; i++) yield return (ranked[i].a, ranked[i].b);
        }

        /// <summary>
        /// ドアセル A からドアセル B までの経路を探す。途中は部屋の外だけを通る。
        /// 返す経路は両端のドアセルを含む。見つからなければ null。
        /// </summary>
        private List<GridPos> FindPath(PlacedSocket socketA, PlacedSocket socketB)
        {
            var start = socketA.Cell;
            var goal = socketB.Cell;
            if (!InsideGrid(start) || !InsideGrid(goal)) return null;

            // ドアの外に 1 歩出られないソケットは使えない。
            if (!InsideGrid(socketA.ExitCell) || !InsideGrid(socketB.ExitCell)) return null;
            if (socketA.ExitCell != goal && IsRoomCell(socketA.ExitCell)) return null;
            if (socketB.ExitCell != start && IsRoomCell(socketB.ExitCell)) return null;

            int goalIndex = CellIndex(goal);
            int startState = CellIndex(start) * 4 + (int)socketA.Facing;

            Array.Fill(gScore, int.MaxValue);
            Array.Fill(cameFrom, -1);
            open.Clear();

            gScore[startState] = 0;
            open.Push(GridPos.Manhattan(start, goal), startState);

            int turnPenalty = Math.Max(0, parameters.CorridorTurnPenalty);

            while (open.Count > 0)
            {
                int state = open.Pop();
                int cellIndex = state / 4;
                var direction = (Direction)(state & 3);

                if (cellIndex == goalIndex) return Reconstruct(state, startState);

                int cost = gScore[state];
                var cell = CellOf(cellIndex);

                foreach (var step in DirectionUtil.All)
                {
                    // ドアからは必ず正面に出る。横に抜けると壁をすり抜けたように見えてしまう。
                    if (state == startState && step != socketA.Facing) continue;

                    var next = cell + step.Offset();
                    if (!InsideGrid(next)) continue;

                    // 通ってよいのは空きセルと、ゴールのドアセルだけ。
                    int nextIndex = CellIndex(next);
                    if (roomGrid[nextIndex] != Blocked && nextIndex != goalIndex) continue;
                    if (nextIndex == goalIndex && step != socketB.Facing.Opposite()) continue;

                    int nextCost = cost + 1 + (step == direction ? 0 : turnPenalty);
                    int nextState = nextIndex * 4 + (int)step;
                    if (nextCost >= gScore[nextState]) continue;

                    gScore[nextState] = nextCost;
                    cameFrom[nextState] = state;
                    open.Push(nextCost + GridPos.Manhattan(next, goal), nextState);
                }
            }

            return null;
        }

        private List<GridPos> Reconstruct(int goalState, int startState)
        {
            var reversed = new List<GridPos>();
            int state = goalState;
            while (state != -1)
            {
                reversed.Add(CellOf(state / 4));
                if (state == startState) break;
                state = cameFrom[state];
            }
            reversed.Reverse();
            return reversed;
        }

        private bool InsideGrid(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < width && p.Y < height;
        private bool IsRoomCell(GridPos p) => roomGrid[CellIndex(p)] != Blocked;
        private int CellIndex(GridPos p) => p.Y * width + p.X;
        private GridPos CellOf(int index) => new GridPos(index % width, index / width);
    }
}
