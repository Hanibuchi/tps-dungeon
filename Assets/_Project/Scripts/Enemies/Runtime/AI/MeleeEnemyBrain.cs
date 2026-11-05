using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 近づいて殴る敵（01 単純近距離、03 浮遊近距離）。浮くかどうかは <see cref="EnemyMotor"/> の高さで決まり、動きは同じ。
    ///
    ///   追う → 間合い（<see cref="attackRange"/>）に入ったら止まって Attack を流す（浮く敵は先に降りる）
    ///   → <see cref="windup"/> 秒後に体の前の球（<see cref="hitRadius"/>）の中の相手に当てる → <see cref="recovery"/> 秒の隙 → 間を置いて次
    /// 溜めの間は相手の方へ回るが、当たりの瞬間の向きで当てるので、横へ逃げれば避けられる。
    /// 浮く敵（03）は追う間ずっと高く飛んでいて、間合いに入って殴れるときだけ降りてきて（<see cref="EnemyMotor.Descend"/>）、降りきってから溜める。
    /// 降りる間に相手が間合いから離れたら、殴るのをやめてまた上がる。殴り終えて隙が明けたら上がる。降りている間が反撃の機会。
    /// スタン・気絶で溜めは取り消す。
    /// 敵のプレハブのルートに付ける。強さの値は Enemies.csv から入る（EnemyStatsWriter）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Melee)")]
    public sealed class MeleeEnemyBrain : EnemyBrain
    {
        private enum Phase { Chase, Dive, Windup, Recover }

        /// <summary>降りる間に相手がこれだけ間合いの外へ出たら、殴るのをやめて上がる（m）。</summary>
        private const float DiveSlack = 1f;

        /// <summary>これだけ降りても降りきれなければ、殴るのをやめて上がる（秒）。</summary>
        private const float DiveTimeout = 1f;

        /// <summary>降りるのをやめてから、また降りられるまで（秒）。</summary>
        private const float DiveRetry = 0.5f;

        [Header("近接")]
        [SerializeField, Min(0.3f), Tooltip("相手の足元までこの距離に入ったら殴り始める（m）。")]
        private float attackRange = 1.6f;

        [SerializeField, Min(0.1f), Tooltip("当たりの球の半径（m）。体の前、間合いの半分ほどの所に置く。")]
        private float hitRadius = 0.9f;

        [SerializeField, Min(0f), Tooltip("Attack を流してから当たるまで（秒）。")]
        private float windup = 0.45f;

        [SerializeField, Min(0f), Tooltip("当てたあと動き出すまで（秒）。")]
        private float recovery = 0.5f;

        [Header("ボスの段階（0 なら使わない）")]
        [SerializeField, Min(0), Tooltip("この段階から、振り下ろした所から衝撃の輪が広がる（範囲ダメージ）。")]
        private int shockwaveStage;

        [SerializeField, Min(0.5f), Tooltip("衝撃の輪の半径（m）。")]
        private float shockwaveRadius = 4f;

        [SerializeField, Range(0f, 2f), Tooltip("衝撃の輪の中心でのダメージ（1 回の攻撃のダメージに掛ける）。")]
        private float shockwaveDamageScale = 0.6f;

        [SerializeField, Tooltip("衝撃の輪の見た目。")]
        private GameObject shockwaveEffect;

        [SerializeField, Min(0.01f)]
        private float shockwaveEffectScale = 1f;

        [SerializeField, Min(0), Tooltip("この段階から、殴ったあと間を置かずにもう 1 回殴る（溜めは半分）。")]
        private int comboStage;

        private readonly List<EnemyTarget> hits = new List<EnemyTarget>();
        private Phase phase;
        private float timer;
        // 連撃の 2 発目を溜めている。
        private bool comboSwing;

        protected override void OnEnable()
        {
            base.OnEnable();
            phase = Phase.Chase;
        }

        protected override void Think(float dt)
        {
            // 浮く敵は、殴るために降りる間と殴っている間だけ降りる。追う間は飛んだまま。
            Motor.Descend = phase != Phase.Chase;

            switch (phase)
            {
                case Phase.Chase:
                    Chase();
                    break;

                case Phase.Dive:
                    Dive(dt);
                    break;

                case Phase.Windup:
                    if (HasTarget) Motor.LookAt = Target.Position;
                    timer -= dt;
                    if (timer <= 0f) Strike();
                    break;

                case Phase.Recover:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        phase = Phase.Chase;
                        CooldownRemaining = Cooldown;
                    }
                    break;
            }
        }

        private void Chase()
        {
            Motor.LookAt = null;
            Motor.Destination = ChasePoint();
            Motor.StopDistance = attackRange * 0.75f;

            if (!HasTarget || !TargetVisible || CooldownRemaining > 0f || TargetDistance > attackRange) return;

            if (Motor.IsDown) StartWindup();
            else
            {
                phase = Phase.Dive;
                timer = DiveTimeout;
                Motor.Descend = true;
            }
        }

        /// <summary>浮く敵が殴りに降りる間。相手の方へ寄りながら降り、降りきったら溜める。</summary>
        private void Dive(float dt)
        {
            timer -= dt;
            if (!HasTarget || !TargetVisible || TargetDistance > attackRange + DiveSlack || timer <= 0f)
            {
                // すぐまた降りて上下を繰り返さないよう、少し間を置く。
                phase = Phase.Chase;
                Motor.LookAt = null;
                CooldownRemaining = Mathf.Max(CooldownRemaining, DiveRetry);
                return;
            }

            Motor.Destination = ChasePoint();
            Motor.StopDistance = attackRange * 0.75f;
            Motor.LookAt = Target.Position;
            if (Motor.IsDown && TargetDistance <= attackRange) StartWindup();
        }

        private void StartWindup(bool combo = false)
        {
            comboSwing = combo;
            phase = Phase.Windup;
            timer = Scaled(combo ? windup * 0.5f : windup);
            Motor.Destination = null;
            Motor.LookAt = Target.Position;
            PlayAttack();
            PlaySound(windupClip, Center);
            StartWindupEffect(HeadPoint);
        }

        private void Strike()
        {
            phase = Phase.Recover;
            timer = Scaled(recovery);
            Motor.LookAt = null;
            StopWindupEffect();

            // 球は体の前、高さは体の中心と相手の胸の間（浮いている敵が下の相手に届くように）。
            Vector3 center = Center + transform.forward * (attackRange * 0.5f);
            if (HasTarget) center.y = Mathf.Lerp(center.y, Target.AimPoint.y, 0.5f);

            PlaySound(attackClip, center);
            EnemyHits.Collect(center, hitRadius, hits);
            foreach (EnemyTarget target in hits)
            {
                target.Damage(damage, gameObject);
                PlaySound(hitClip, target.AimPoint);
                SpawnHitEffect(target.AimPoint);
            }

            if (StageReached(shockwaveStage)) Shockwave(center);

            // 連撃: 1 発目のあと、相手がまだ近ければ隙を置かずに 2 発目を溜める。
            if (StageReached(comboStage) && !comboSwing && HasTarget && TargetVisible && TargetDistance <= attackRange * 1.5f)
            {
                StartWindup(true);
                return;
            }
            comboSwing = false;
        }

        /// <summary>振り下ろした所の足元から衝撃の輪を広げる。中心ほど痛く、壁の向こうには届かない。</summary>
        private void Shockwave(Vector3 strikePoint)
        {
            Vector3 ground = EnemyMotor.OnNavMesh(strikePoint, 3f);
            Vector3 origin = ground + Vector3.up * 0.5f;
            EnemyEffects.Spawn(shockwaveEffect, ground, Quaternion.identity, shockwaveEffectScale, 3f);

            int center = Mathf.RoundToInt(damage * shockwaveDamageScale);
            EnemyHits.Collect(origin, shockwaveRadius + EnemyTarget.DecoyRadius, hits);
            foreach (EnemyTarget target in hits)
            {
                Vector3 aim = target.AimPoint;
                if (!EnemyHits.Clear(origin, aim, transform)) continue;
                // 跳べば避けられるように、足元から高い所に居る相手には当てない。
                if (target.Position.y - ground.y > 1.2f) continue;
                int dealt = EnemyAttackRules.ExplosionDamage(center, Vector3.Distance(origin, aim), shockwaveRadius, 0.4f);
                if (dealt > 0) target.Damage(dealt, gameObject);
            }
        }

        public override string StateLabel => phase switch
        {
            Phase.Dive => "降りる",
            Phase.Windup => $"溜め {timer:0.0}s",
            Phase.Recover => $"隙 {timer:0.0}s",
            _ => HasTarget ? "追う" : "待つ",
        };

        protected override void DrawRanges()
        {
            Disc(transform.position, attackRange, new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        protected override void Interrupted()
        {
            if (phase == Phase.Chase) return;
            bool attacking = phase != Phase.Dive;
            phase = Phase.Chase;
            comboSwing = false;
            Motor.LookAt = null;
            if (!attacking) return;
            CooldownRemaining = Mathf.Max(CooldownRemaining, Cooldown * 0.5f);
        }
    }
}
