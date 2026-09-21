using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>フロアに配置済みのドア候補。Cell はフロア絶対座標で、部屋の外周セルを指す。</summary>
    public sealed class PlacedSocket
    {
        public GridPos Cell { get; }
        public Direction Facing { get; }

        /// <summary>このソケットを使っている辺の番号。未使用なら -1。</summary>
        public int UsedByEdge { get; internal set; } = -1;

        public bool IsUsed => UsedByEdge >= 0;

        /// <summary>ソケットの 1 つ外のセル。ドアとして使われている場合は隣の部屋のセルになる。</summary>
        public GridPos ExitCell => Cell + Facing.Offset();

        public PlacedSocket(GridPos cell, Direction facing)
        {
            Cell = cell;
            Facing = facing;
        }

        public override string ToString() => $"{Cell}->{Facing}{(IsUsed ? $" (edge {UsedByEdge})" : "")}";
    }

    /// <summary>フロアに配置された部屋 1 つ分。</summary>
    public sealed class RoomInstance
    {
        public int Index { get; }
        public RoomTemplateData Template { get; }

        /// <summary>時計回りの 90 度回転ステップ数（0-3）。</summary>
        public int Rotation { get; }

        /// <summary>回転を反映した後のフロア絶対矩形。</summary>
        public GridRect Bounds { get; }

        public RoomRole Role { get; internal set; }
        public IReadOnlyList<PlacedSocket> Sockets { get; }

        /// <summary>階段/ショップなど役割マーカーを置くセル。既定は部屋の中心。</summary>
        public GridPos FeatureCell { get; internal set; }

        public RoomInstance(
            int index,
            RoomTemplateData template,
            int rotation,
            GridRect bounds,
            IReadOnlyList<PlacedSocket> sockets)
        {
            Index = index;
            Template = template;
            Rotation = rotation;
            Bounds = bounds;
            Sockets = sockets;
            Role = RoomRole.Normal;
            FeatureCell = bounds.Center;
        }

        public IEnumerable<PlacedSocket> UsedSockets()
        {
            foreach (var socket in Sockets)
            {
                if (socket.IsUsed) yield return socket;
            }
        }

        public override string ToString() => $"Room{Index}({Template.Id} r{Rotation} {Bounds} {Role})";
    }
}
