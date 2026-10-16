using System;

namespace TpsDungeon.Map.Data
{
    /// <summary>
    /// ダンジョンの中の 1 つの階の位置。Layer は層の番号（0 始まり）、Floor はその層の何階目（0 始まり）。
    /// 層のランダムな階の数と同じ番号はボス階（DungeonRoute が決める）。
    /// </summary>
    public readonly struct DungeonFloor : IEquatable<DungeonFloor>
    {
        public readonly int Layer;
        public readonly int Floor;

        public DungeonFloor(int layer, int floor)
        {
            Layer = layer;
            Floor = floor;
        }

        public bool Equals(DungeonFloor other) => Layer == other.Layer && Floor == other.Floor;
        public override bool Equals(object obj) => obj is DungeonFloor other && Equals(other);
        public override int GetHashCode() => unchecked((Layer * 397) ^ Floor);
        public static bool operator ==(DungeonFloor a, DungeonFloor b) => a.Equals(b);
        public static bool operator !=(DungeonFloor a, DungeonFloor b) => !a.Equals(b);
        public override string ToString() => $"L{Layer + 1}-{Floor + 1}";
    }
}
