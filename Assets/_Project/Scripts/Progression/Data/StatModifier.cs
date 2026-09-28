using System;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// ステータスへの補正。(値 + Flat) × (1 + Percent)。永続アップグレードなどが外から与える。
    /// 既定値（両方 0）は何もしない。
    /// </summary>
    [Serializable]
    public struct StatModifier : IEquatable<StatModifier>
    {
        public static readonly StatModifier None = default;

        /// <summary>足す量。</summary>
        public float Flat;

        /// <summary>割合の上乗せ。0.1 で +10%。</summary>
        public float Percent;

        public StatModifier(float flat, float percent)
        {
            Flat = flat;
            Percent = percent;
        }

        public double Apply(double value) => (value + Flat) * (1.0 + Percent);

        public bool Equals(StatModifier other) => Flat.Equals(other.Flat) && Percent.Equals(other.Percent);

        public override bool Equals(object obj) => obj is StatModifier other && Equals(other);

        public override int GetHashCode() => (Flat.GetHashCode() * 397) ^ Percent.GetHashCode();
    }
}
