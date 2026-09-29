using TpsDungeon.Enemies;

namespace TpsDungeon.Combat
{
    /// <summary>近接攻撃のダメージの出どころ。</summary>
    public enum MeleeHitKind
    {
        /// <summary>段の 1 撃（振り・走り・叩きつけの本撃）。</summary>
        Hit,

        /// <summary>爆発のエンチャント。</summary>
        Explosion,

        /// <summary>叩きつけから走る衝撃波（数のエンチャント）。</summary>
        Shockwave,

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

        public MeleeHitRecord(MeleeHitKind kind, EnemyHealth enemy, int step, int damage, int dealt, bool isCritical)
        {
            Kind = kind;
            Enemy = enemy;
            Step = step;
            Damage = damage;
            Dealt = dealt;
            IsCritical = isCritical;
        }
    }
}
