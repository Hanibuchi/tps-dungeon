using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>敵に与える 1 回分のダメージ。当たった場所と向きは気絶で倒れ込む向きに使う。</summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;

        /// <summary>当たった場所（ワールド）。分からなければ null。</summary>
        public readonly Vector3? Point;

        /// <summary>攻撃が進む向き（ワールド、長さは問わない）。分からなければゼロ。</summary>
        public readonly Vector3 Direction;

        public DamageInfo(int amount, Vector3? point = null, Vector3 direction = default)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
        }

        public static implicit operator DamageInfo(int amount) => new DamageInfo(amount);
    }
}
