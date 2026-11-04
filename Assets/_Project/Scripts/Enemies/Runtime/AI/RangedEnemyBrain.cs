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
    /// 敵のプレハブのルートに付ける。強さの値は Enemies.csv から入る（EnemyStatsWriter）。
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

        [SerializeField, Tooltip("撃ち出した瞬間に撃ち出す所へ出すエフェクト。")]
        private GameObject muzzleEffect;

        [SerializeField, Tooltip("弾が当たった所に出すエフェクト。")]
        private GameObject impactEffect;

        [SerializeField, Min(0.01f)]
        private float impactEffectScale = 0.6f;

        [SerializeField, Tooltip("撃ち出す所。体の中心から前へこの距離（m）。")]
        private float muzzleForward = 0.5f;

        [Header("ボスの段階（0 なら使わない）")]
        [SerializeField, Min(0), Tooltip("この段階から、扇に広げて撃つ（spreadCount 発）。")]
        private int spreadStage;

        [SerializeField, Min(1), Tooltip("扇に広げて撃つ数。")]
        private int spreadCount = 3;

        [SerializeField, Min(0), Tooltip("この段階から、扇の数を wideSpreadCount に増やす。")]
        private int wideSpreadStage;

        [SerializeField, Min(1), Tooltip("増やした扇の数。")]
        private int wideSpreadCount = 5;

        [SerializeField, Range(1f, 45f), Tooltip("扇の隣り合う弾の間の角度（度）。")]
        private float spreadAngle = 14f;

        [SerializeField, Min(0), Tooltip("この段階から、溜めたあと続けて burstCount 回撃つ。")]
        private int burstStage;

        [SerializeField, Min(1), Tooltip("続けて撃つ回数。")]
        private int burstCount = 3;

        [SerializeField, Min(0.05f), Tooltip("続けて撃つ間（秒）。")]
        private float burstInterval = 0.22f;

        /// <summary>撃ち出す所（体の中心から前へ）。</summary>
        private Vector3 Muzzle => Center + transform.forward * muzzleForward;

        private bool aiming;
        private float timer;
        // 続けて撃つ残りの回数と、次まで。
        private int burstRemaining;
        private float burstTimer;
        private RangedStance stance;

        protected override void OnEnable()
        {
            base.OnEnable();
            aiming = false;
            burstRemaining = 0;
        }

        protected override void Think(float dt)
        {
            if (burstRemaining > 0)
            {
                if (!HasTarget || !TargetVisible)
                {
                    burstRemaining = 0;
                }
                else
                {
                    Motor.Destination = null;
                    Motor.LookAt = Target.Position;
                    burstTimer -= dt;
                    if (burstTimer <= 0f)
                    {
                        burstRemaining--;
                        burstTimer = Scaled(burstInterval);
                        Shoot();
                    }
                    return;
                }
            }

            stance = HasTarget
                ? EnemyAttackRules.Stance(TargetDistance, TargetVisible, minRange, maxRange)
                : RangedStance.Approach;

            if (aiming)
            {
                // 溜めの途中で見失ったり離れすぎたりしたら取りやめる。近すぎるのはそのまま撃つ。
                if (!HasTarget || stance == RangedStance.Approach)
                {
                    aiming = false;
                    StopWindupEffect();
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
            timer = Scaled(windup);
            PlaySound(windupClip, Center);
            StartWindupEffect(Muzzle);
        }

        private void Fire()
        {
            aiming = false;
            CooldownRemaining = Cooldown;
            StopWindupEffect();
            Shoot();

            if (StageReached(burstStage) && burstCount > 1)
            {
                burstRemaining = burstCount - 1;
                burstTimer = Scaled(burstInterval);
            }
        }

        /// <summary>今の段階の数だけ、相手の胸へ向けて扇に撃つ。</summary>
        private void Shoot()
        {
            PlayAttack();
            Vector3 muzzle = Muzzle;
            Vector3 direction = Target.AimPoint - muzzle;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;
            EnemyEffects.Spawn(muzzleEffect, muzzle, Quaternion.LookRotation(direction, Vector3.up), 1f, 1f);

            int count = StageReached(wideSpreadStage) ? wideSpreadCount : StageReached(spreadStage) ? spreadCount : 1;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * spreadAngle;
                Launch(muzzle, Quaternion.AngleAxis(angle, Vector3.up) * direction);
            }
            PlaySound(attackClip, muzzle);
        }

        private void Launch(Vector3 muzzle, Vector3 direction)
        {
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
        }

        public override string StateLabel
        {
            get
            {
                if (aiming) return $"狙う {timer:0.0}s";
                if (burstRemaining > 0) return $"連射 残り {burstRemaining}";
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
            burstRemaining = 0;
            if (!aiming) return;
            aiming = false;
            Motor.LookAt = null;
            CooldownRemaining = Mathf.Max(CooldownRemaining, Cooldown * 0.5f);
        }
    }
}
