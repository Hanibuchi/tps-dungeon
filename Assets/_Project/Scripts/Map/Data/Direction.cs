using System;

namespace TpsDungeon.Map.Data
{
    /// <summary>グリッド上の四方向。North が +Y、East が +X。</summary>
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class DirectionUtil
    {
        /// <summary>North, East, South, West の固定順。列挙順は生成の決定性に影響するので変えないこと。</summary>
        public static readonly Direction[] All =
        {
            Direction.North,
            Direction.East,
            Direction.South,
            Direction.West,
        };

        public static GridPos Offset(this Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return new GridPos(0, 1);
                case Direction.East: return new GridPos(1, 0);
                case Direction.South: return new GridPos(0, -1);
                case Direction.West: return new GridPos(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(dir));
            }
        }

        public static Direction Opposite(this Direction dir) => (Direction)(((int)dir + 2) & 3);

        /// <summary>時計回りに 90 度 × steps だけ回した向き。</summary>
        public static Direction RotateCw(this Direction dir, int steps) => (Direction)((((int)dir + steps) % 4 + 4) % 4);
    }
}
