using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>敵に与える 1 回分のダメージ。当たった場所と向きは気絶で倒れ込む向きとノックバックに使う。</summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;

        /// <summary>当たった場所（ワールド）。分からなければ null。</summary>
        public readonly Vector3? Point;

        /// <summary>攻撃が進む向き（ワールド、長さは問わない）。分からなければゼロ。</summary>
        public readonly Vector3 Direction;

        /// <summary>クリティカルだったか。表示や音を変えたいとき用。</summary>
        public readonly bool IsCritical;

        /// <summary>攻撃の向きに押し出す速さ（m/s）。0 なら押さない。</summary>
        public readonly float Knockback;

        /// <summary>スタン・気絶の判定で相対ダメージ量に掛ける倍率（スタンのエンチャント）。1 で素のまま。</summary>
        public readonly float ReactionScale;

        public DamageInfo(int amount, Vector3? point = null, Vector3 direction = default,
            bool isCritical = false, float knockback = 0f, float reactionScale = 1f)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            IsCritical = isCritical;
            Knockback = knockback;
            ReactionScale = reactionScale;
        }

        public static implicit operator DamageInfo(int amount) => new DamageInfo(amount);
    }
}
