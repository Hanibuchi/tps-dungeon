using System;

namespace TpsDungeon.Map.Data
{
    /// <summary>セル単位の矩形。X/Y は左下端のセル、Width/Height はセル数。</summary>
    public readonly struct GridRect : IEquatable<GridRect>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public GridRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int MaxX => X + Width - 1;
        public int MaxY => Y + Height - 1;
        public int Area => Width * Height;

        /// <summary>矩形の中心セル（偶数サイズのときは切り捨て側）。</summary>
        public GridPos Center => new GridPos(X + Width / 2, Y + Height / 2);

        public bool Contains(GridPos p) => p.X >= X && p.X <= MaxX && p.Y >= Y && p.Y <= MaxY;

        public bool Overlaps(GridRect other) =>
            X <= other.MaxX && other.X <= MaxX && Y <= other.MaxY && other.Y <= MaxY;

        /// <summary>四辺から amount セルずつ内側に縮めた矩形。潰れる場合は Width/Height が 0 以下になる。</summary>
        public GridRect Shrink(int amount) =>
            new GridRect(X + amount, Y + amount, Width - amount * 2, Height - amount * 2);

        public bool Equals(GridRect other) =>
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is GridRect other && Equals(other);

        public override int GetHashCode() => unchecked((((X * 397) ^ Y) * 397 ^ Width) * 397 ^ Height);

        public override string ToString() => $"[{X},{Y} {Width}x{Height}]";

        /// <summary>
        /// 2 矩形の最短距離（軸ごとのセル数の差の合計）。
        /// 重なっていれば 0、辺を共有して隣り合っていれば 1、角だけ触れていれば 2 になる。
        /// </summary>
        public static int Distance(GridRect a, GridRect b)
        {
            int dx = Math.Max(0, Math.Max(a.X - b.MaxX, b.X - a.MaxX));
            int dy = Math.Max(0, Math.Max(a.Y - b.MaxY, b.Y - a.MaxY));
            return dx + dy;
        }
    }
}
