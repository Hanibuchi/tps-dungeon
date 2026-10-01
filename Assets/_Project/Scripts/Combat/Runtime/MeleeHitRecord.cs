using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>近接攻撃のダメージの出どころ。</summary>
    public enum MeleeHitKind
    {
        /// <summary>段の 1 撃（振り・走り・叩きつけ。数のエンチャントで増えた叩きつけも）。</summary>
        Hit,

        /// <summary>爆発のエンチャント。</summary>
        Explosion,

        /// <summary>叩きつけの追撃（多重のエンチャント）。</summary>
        FollowUp,
    }

    /// <summary>近接攻撃で敵に当てた 1 回分。MeleeAttacker.Dealt で知らせる。</summary>
    public readonly struct MeleeHitRecord
    {
        public readonly MeleeHitKind Kind;
        public readonly EnemyHealth Enemy;

        /// <summary>出した段（0 始まり）。</summary>
        public readonly int Step;

        /// <summary>与えようとしたダメージ（クリティカル込み）。</summary>
        public readonly int Damage;

        /// <summary>実際に減った HP（残り HP で頭打ち）。</summary>
        public readonly int Dealt;

        public readonly bool IsCritical;

        /// <summary>当たった場所（ワールド）。ダメージの数字をここに出す。</summary>
        public readonly Vector3 Point;

        /// <summary>
        /// コンボの数（この 1 撃の段を含めて、続けて当てた段の数）。ダメージの数字に「N COMBO」と添える。
        /// コンボボーナスの上乗せが乗っていない 1 撃（ボーナスの無い武器、コンボの頭、爆発などの段の 1 撃でないもの）は 0。
        /// </summary>
        public readonly int Combo;

        public MeleeHitRecord(MeleeHitKind kind, EnemyHealth enemy, int step, int damage, int dealt, bool isCritical, Vector3 point,
            int combo = 0)
        {
            Kind = kind;
            Enemy = enemy;
            Step = step;
            Damage = damage;
            Dealt = dealt;
            IsCritical = isCritical;
            Point = point;
            Combo = combo;
        }
    }
}
