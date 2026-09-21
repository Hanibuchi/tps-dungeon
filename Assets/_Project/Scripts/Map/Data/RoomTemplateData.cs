using System;
using System.Collections.Generic;

namespace TpsDungeon.Map.Data
{
    /// <summary>部屋テンプレート上のドア候補地点。セルは部屋ローカル、Facing は外向き。</summary>
    public readonly struct DoorSocketData : IEquatable<DoorSocketData>
    {
        public readonly GridPos LocalCell;
        public readonly Direction Facing;

        public DoorSocketData(GridPos localCell, Direction facing)
        {
            LocalCell = localCell;
            Facing = facing;
        }

        public bool Equals(DoorSocketData other) => LocalCell == other.LocalCell && Facing == other.Facing;
        public override bool Equals(object obj) => obj is DoorSocketData other && Equals(other);
        public override int GetHashCode() => unchecked((LocalCell.GetHashCode() * 397) ^ (int)Facing);
        public override string ToString() => $"{LocalCell}->{Facing}";
    }

    /// <summary>
    /// 部屋テンプレートの、Unity に依存しないデータ表現。
    /// Prefab 側の RoomTemplate コンポーネントからこの形に変換して生成器に渡す。
    /// </summary>
    public sealed class RoomTemplateData
    {
        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public RoomTag Tags { get; }
        public bool AllowRotation { get; }
        public float Weight { get; }
        public IReadOnlyList<DoorSocketData> Sockets { get; }

        public RoomTemplateData(
            string id,
            int width,
            int height,
            RoomTag tags,
            bool allowRotation,
            float weight,
            IReadOnlyList<DoorSocketData> sockets)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException($"テンプレート '{id}' のサイズが不正: {width}x{height}");
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Width = width;
            Height = height;
            Tags = tags;
            AllowRotation = allowRotation;
            Weight = weight > 0f ? weight : 0f;
            Sockets = sockets ?? Array.Empty<DoorSocketData>();
        }

        /// <summary>時計回り rotation ステップ（0-3）回した後のサイズ。</summary>
        public (int width, int height) SizeAfterRotation(int rotation) =>
            (rotation & 1) == 0 ? (Width, Height) : (Height, Width);

        /// <summary>時計回り rotation ステップ回した後のソケット一覧。順序はテンプレート定義順のまま。</summary>
        public List<DoorSocketData> SocketsAfterRotation(int rotation)
        {
            int steps = ((rotation % 4) + 4) % 4;
            var result = new List<DoorSocketData>(Sockets.Count);
            foreach (var socket in Sockets)
            {
                result.Add(new DoorSocketData(RotateCell(socket.LocalCell, steps), socket.Facing.RotateCw(steps)));
            }
            return result;
        }

        /// <summary>
        /// 部屋ローカルのセルを時計回りに steps 回まわす。
        /// Direction.RotateCw と同じ向き（North ベクトル (0,1) が East ベクトル (1,0) に移る）でなければ、
        /// ソケットのセルと向きが食い違って壁の中にドアができてしまう。
        /// </summary>
        public GridPos RotateCell(GridPos cell, int steps)
        {
            switch (((steps % 4) + 4) % 4)
            {
                case 0: return cell;
                case 1: return new GridPos(cell.Y, Width - 1 - cell.X);
                case 2: return new GridPos(Width - 1 - cell.X, Height - 1 - cell.Y);
                default: return new GridPos(Height - 1 - cell.Y, cell.X);
            }
        }

        /// <summary>四辺すべてにソケットがあるか。生成時にどの方向へも接続できる条件。</summary>
        public bool HasSocketOnEverySide()
        {
            int mask = 0;
            foreach (var socket in Sockets) mask |= 1 << (int)socket.Facing;
            return mask == 0b1111;
        }
    }
}
