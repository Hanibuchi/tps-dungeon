using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 射線が通る所まで来て撃つ敵（06 浮遊遠距離、07 遠距離）。浮くかどうかは <see cref="EnemyMotor"/> の高さで決まり、動きは同じ。
    ///
    /// 立ち位置は <see cref="EnemyAttackRules.Stance"/> で決める:
    ///   見えていない・遠い → 経路で寄る（回り込めば見える所へ出る。見失ったら最後に見た所へ）
    ///   見えていて射程の中 → 止まって相手を向き、溜めて（<see cref="windup"/> 秒）撃つ
    ///   近すぎる → 相手から離れる向きへ下がる（見えていれば撃つ）
    /// 弾（<see cref="EnemyProjectile"/>）は溜め終わりの相手の胸へまっすぐ撃つ。先読みはしないので、動いていれば避けられる。
    /// 溜めの間のスタン・気絶で取り消す。
    /// MonsterBuilder がプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Ranged)")]
    public sealed class RangedEnemyBrain : EnemyBrain
    {
        [Header("遠距離")]
        [SerializeField, Min(0f), Tooltip("これより近いと下がる（m）。")]
        private float minRange = 4f;

        [SerializeField, Min(1f), Tooltip("射程（m）。これより遠いと寄る。")]
        private float maxRange = 12f;

        [SerializeField, Min(0f), Tooltip("止まって相手を向いてから撃つまで（秒）。")]
        private float windup = 0.7f;

        [SerializeField, Min(0.5f), Tooltip("下がるときに一度に離れる距離（m）。")]
        private float retreatStep = 3f;

        [Header("弾")]
        [SerializeField, Min(1f), Tooltip("弾の速さ（m/秒）。")]
        private float projectileSpeed = 12f;

        [SerializeField, Min(0.01f), Tooltip("弾の当たりの半径（m）。")]
        private float projectileRadius = 0.25f;

        [SerializeField, Tooltip("弾の見た目（ループする粒の素材）。")]
        private GameObject projectileEffect;

        [SerializeField, Min(0.01f)]
        private float projectileEffectScale = 0.5f;

        [SerializeField, Tooltip("弾が当たった所に出すエフェクト。")]
        private GameObject impactEffect;

        [SerializeField, Min(0.01f)]
        private float impactEffectScale = 0.6f;

        [SerializeField, Tooltip("撃ち出す所。体の中心から前へこの距離（m）。")]
        private float muzzleForward = 0.5f;

        private bool aiming;
        private float timer;
        private RangedStance stance;

        protected override void OnEnable()
        {
            base.OnEnable();
            aiming = false;
        }

        protected override void Think(float dt)
        {
            stance = HasTarget
                ? EnemyAttackRules.Stance(TargetDistance, TargetVisible, minRange, maxRange)
                : RangedStance.Approach;

            if (aiming)
            {
                // 溜めの途中で見失ったり離れすぎたりしたら取りやめる。近すぎるのはそのまま撃つ。
                if (!HasTarget || stance == RangedStance.Approach)
                {
                    aiming = false;
                }
                else
                {
                    Motor.Destination = null;
                    Motor.LookAt = Target.Position;
                    timer -= dt;
                    if (timer <= 0f && Motor.IsFacing(Target.Position, 30f)) Fire();
                    return;
                }
            }

            switch (stance)
            {
                case RangedStance.Approach:
                    Motor.LookAt = null;
                    Motor.Destination = ChasePoint();
                    // 見えていないなら相手の所まで回り込む。見えていれば射程の少し内側まで。
                    Motor.StopDistance = TargetVisible ? maxRange * 0.8f : 0.5f;
                    break;

                case RangedStance.Hold:
                    Motor.Destination = null;
                    Motor.LookAt = Target.Position;
                    TryStartAim();
                    break;

                case RangedStance.Retreat:
                    Vector3 away = Flat(transform.position - Target.Position);
                    if (away.sqrMagnitude < 1e-4f) away = -transform.forward;
                    Motor.Destination = transform.position + away.normalized * retreatStep;
                    Motor.StopDistance = 0.3f;
                    Motor.LookAt = Target.Position;
                    TryStartAim();
                    break;
            }
        }

        private void TryStartAim()
        {
            if (CooldownRemaining > 0f) return;
            aiming = true;
            timer = windup;
            PlaySound(windupClip, Center);
        }

        private void Fire()
        {
            aiming = false;
            CooldownRemaining = cooldown;
            Play(AttackParam);

            Vector3 muzzle = Center + transform.forward * muzzleForward;
            Vector3 direction = Target.AimPoint - muzzle;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;

            EnemyProjectile.Launch(projectileEffect, projectileEffectScale, muzzle, direction, new EnemyProjectile.Settings
            {
                Speed = projectileSpeed,
                Range = maxRange * 1.5f,
                Radius = projectileRadius,
                Damage = damage,
                Owner = transform,
                HitEffect = impactEffect,
                HitEffectScale = impactEffectScale,
                HitClip = hitClip,
                Volume = volume,
            });
            PlaySound(attackClip, muzzle);
        }

        public override string StateLabel
        {
            get
            {
                if (aiming) return $"狙う {timer:0.0}s";
                if (!HasTarget) return "待つ";
                return stance switch
                {
                    RangedStance.Hold => "止まる",
                    RangedStance.Retreat => "下がる",
                    _ => TargetVisible ? "寄る" : "回り込む",
                };
            }
        }

        protected override void DrawRanges()
        {
            Disc(transform.position, minRange, new Color(0.3f, 0.6f, 1f, 0.9f));
            Disc(transform.position, maxRange, new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        protected override void Interrupted()
        {
            if (!aiming) return;
            aiming = false;
            Motor.LookAt = null;
            CooldownRemaining = Mathf.Max(CooldownRemaining, cooldown * 0.5f);
        }
    }
}
