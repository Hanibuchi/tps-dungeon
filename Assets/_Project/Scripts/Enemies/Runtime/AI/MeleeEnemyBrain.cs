using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 近づいて殴る敵（01 単純近距離、03 浮遊近距離）。浮くかどうかは <see cref="EnemyMotor"/> の高さで決まり、動きは同じ。
    ///
    ///   追う → 間合い（<see cref="attackRange"/>）に入ったら止まって Attack を流す
    ///   → <see cref="windup"/> 秒後に体の前の球（<see cref="hitRadius"/>）の中の相手に当てる → <see cref="recovery"/> 秒の隙 → 間を置いて次
    /// 溜めの間は相手の方へ回るが、当たりの瞬間の向きで当てるので、横へ逃げれば避けられる。
    /// スタン・気絶で溜めは取り消す。
    /// MonsterBuilder がプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Melee)")]
    public sealed class MeleeEnemyBrain : EnemyBrain
    {
        private enum Phase { Chase, Windup, Recover }

        [Header("近接")]
        [SerializeField, Min(0.3f), Tooltip("相手の足元までこの距離に入ったら殴り始める（m）。")]
        private float attackRange = 1.6f;

        [SerializeField, Min(0.1f), Tooltip("当たりの球の半径（m）。体の前、間合いの半分ほどの所に置く。")]
        private float hitRadius = 0.9f;

        [SerializeField, Min(0f), Tooltip("Attack を流してから当たるまで（秒）。")]
        private float windup = 0.45f;

        [SerializeField, Min(0f), Tooltip("当てたあと動き出すまで（秒）。")]
        private float recovery = 0.5f;

        private readonly List<EnemyTarget> hits = new List<EnemyTarget>();
        private Phase phase;
        private float timer;

        protected override void OnEnable()
        {
            base.OnEnable();
            phase = Phase.Chase;
        }

        protected override void Think(float dt)
        {
            switch (phase)
            {
                case Phase.Chase:
                    Chase();
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
                        CooldownRemaining = cooldown;
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

            phase = Phase.Windup;
            timer = windup;
            Motor.Destination = null;
            Motor.LookAt = Target.Position;
            Play(AttackParam);
            PlaySound(windupClip, Center);
        }

        private void Strike()
        {
            phase = Phase.Recover;
            timer = recovery;
            Motor.LookAt = null;

            // 球は体の前、高さは体の中心と相手の胸の間（浮いている敵が下の相手に届くように）。
            Vector3 center = Center + transform.forward * (attackRange * 0.5f);
            if (HasTarget) center.y = Mathf.Lerp(center.y, Target.AimPoint.y, 0.5f);

            PlaySound(attackClip, center);
            EnemyHits.Collect(center, hitRadius, hits);
            foreach (EnemyTarget target in hits)
            {
                target.Damage(damage, gameObject);
                PlaySound(hitClip, target.AimPoint);
            }
        }

        public override string StateLabel => phase switch
        {
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
            phase = Phase.Chase;
            Motor.LookAt = null;
            CooldownRemaining = Mathf.Max(CooldownRemaining, cooldown * 0.5f);
        }
    }
}
