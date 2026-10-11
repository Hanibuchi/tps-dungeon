using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 飛びついて攻撃する敵（02 飛びつき近距離）。
    ///
    ///   追う → 間合い（<see cref="leapRange"/>）に入って見えていたら、屈んで溜める（<see cref="crouchTime"/> 秒、Duck を流す）
    ///   → 溜め始めに見た相手の足元へ放物線で飛ぶ。飛び立つと同時に攻撃のアニメーションを流す（飛びかかりながら殴る）。
    ///     agent を止めて自分で動かし、壁に当たったらそこで真下へ落ちる
    ///   → 飛んでいる間に体が触れた相手に 1 回ずつ当てる → 着地して <see cref="landRecovery"/> 秒の隙
    /// 飛ぶ先は溜め始めに決めるので、溜めを見て横へ動けば避けられる。着地の隙が反撃の機会。
    /// 溜めの間のスタン・気絶で取り消す。飛んでいる間にスタンしても止まらず落ちてくる（当たりは無くなる）。気絶なら倒れ込む。
    /// MonsterBuilder がプレハブのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Brain (Leap)")]
    public sealed class LeapEnemyBrain : EnemyBrain
    {
        // 見本（Orc）が持っているクリップ。Big のグループならどのモンスターにもある。
        private const string CrouchParam = "Duck";
        private const string LandParam = "Jump_Land";

        private enum Phase { Chase, Crouch, Air, Land }

        [Header("飛びつき")]
        [SerializeField, Min(0.5f), Tooltip("相手の足元までこの距離に入ったら飛びつく（m）。")]
        private float leapRange = 6f;

        [SerializeField, Min(0f), Tooltip("屈んで溜める時間（秒）。")]
        private float crouchTime = 0.6f;

        [SerializeField, Min(0.5f), Tooltip("飛ぶ水平の速さ（m/秒）。")]
        private float leapSpeed = 9f;

        [SerializeField, Min(0f), Tooltip("放物線の頂点の高さ（m、両端を結んだ線から）。")]
        private float leapApex = 1.4f;

        [SerializeField, Min(0.05f), Tooltip("飛んでいる時間の下限と上限（秒）。")]
        private Vector2 leapTime = new Vector2(0.35f, 0.8f);

        [SerializeField, Min(0.1f), Tooltip("飛んでいる間、体の中心からこの距離に入った相手に当てる（m）。")]
        private float contactRadius = 0.9f;

        [SerializeField, Min(0f), Tooltip("着地してから動き出すまで（秒）。")]
        private float landRecovery = 0.9f;

        [SerializeField, Tooltip("着地の音。")]
        private AudioClip landClip;

        [SerializeField, Tooltip("飛ぶ途中の壁の判定の対象。")]
        private LayerMask wallMask = ~0;

        private readonly List<EnemyTarget> touching = new List<EnemyTarget>();
        private readonly List<EnemyTarget> struck = new List<EnemyTarget>();
        private EnemyRagdoll ragdoll;
        private Phase phase;
        private float timer;
        private float progress;
        private float duration;
        private Vector3 from;
        private Vector3 to;

        protected override void Awake()
        {
            base.Awake();
            ragdoll = GetComponent<EnemyRagdoll>();
        }

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

                case Phase.Crouch:
                    timer -= dt;
                    if (timer <= 0f) TakeOff();
                    break;

                case Phase.Air:
                    Fly(dt, true);
                    break;

                case Phase.Land:
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
            // 間合いの少し内側まで寄る（端で立ち止まって飛べない、を避ける）。
            Motor.StopDistance = leapRange * 0.6f;

            if (!HasTarget || !TargetVisible || CooldownRemaining > 0f || TargetDistance > leapRange) return;

            // 飛ぶ先はここで決める。NavMesh の上に収め、相手との間が壁で遮られていれば飛ばない。
            Vector3 landing = EnemyMotor.OnNavMesh(Target.Position);
            if (!EnemyHits.Clear(Center, landing + Vector3.up * (BodyHeight * 0.5f), transform)) return;

            phase = Phase.Crouch;
            timer = crouchTime;
            to = landing;
            Motor.Destination = null;
            Motor.LookAt = landing;
            Play(CrouchParam);
            PlaySound(windupClip, Center);
        }

        private void TakeOff()
        {
            phase = Phase.Air;
            from = transform.position;
            progress = 0f;
            duration = EnemyAttackRules.LeapDuration(Flat(to - from).magnitude, leapSpeed, leapTime.x, leapTime.y);
            struck.Clear();

            Motor.LookAt = null;
            Motor.Suspend();
            Vector3 heading = Flat(to - from);
            if (heading.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(heading, Vector3.up);

            PlayAttack();
            PlaySound(attackClip, Center);
        }

        /// <summary>放物線に沿って 1 フレーム進める。hurts が偽なら当てない（スタン中）。</summary>
        private void Fly(float dt, bool hurts)
        {
            progress = Mathf.Min(1f, progress + dt / duration);
            Vector3 next = Vector3.Lerp(from, to, progress) + Vector3.up * EnemyAttackRules.LeapHeight(progress, leapApex);

            // 壁に当たったら水平に進むのをやめ、そこから真下へ落ちる（高さの放物線はそのまま）。
            Vector3 step = Flat(next - transform.position);
            float distance = step.magnitude;
            if (distance > 1e-4f && Blocked(step / distance, distance))
            {
                Vector3 here = Flat(transform.position);
                from = new Vector3(here.x, from.y, here.z);
                to = new Vector3(here.x, to.y, here.z);
                next = Vector3.Lerp(from, to, progress) + Vector3.up * EnemyAttackRules.LeapHeight(progress, leapApex);
            }
            transform.position = next;

            if (hurts)
            {
                EnemyHits.Collect(Center, contactRadius, touching);
                foreach (EnemyTarget target in touching)
                {
                    if (!EnemyHits.AddOnce(struck, target)) continue;
                    target.Damage(damage, gameObject);
                    PlaySound(hitClip, target.AimPoint);
                }
            }

            if (progress >= 1f) Land();
        }

        private bool Blocked(Vector3 direction, float distance)
        {
            const float skin = 0.4f;
            Vector3 origin = transform.position + Vector3.up * (BodyHeight * 0.5f);
            var hits = Physics.RaycastAll(origin, direction, distance + skin, wallMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (!EnemyHits.IsSeeThrough(hit.collider, transform)) return true;
            }
            return false;
        }

        private void Land()
        {
            phase = Phase.Land;
            timer = landRecovery;
            Play(LandParam);
            PlaySound(landClip, transform.position);
            Motor.Resume();
        }

        public override string StateLabel => phase switch
        {
            Phase.Crouch => $"屈む {timer:0.0}s",
            Phase.Air => $"飛ぶ {progress:P0}",
            Phase.Land => $"着地の隙 {timer:0.0}s",
            _ => HasTarget ? "追う" : "待つ",
        };

        protected override void DrawRanges()
        {
            Disc(transform.position, leapRange, new Color(1f, 0.3f, 0.2f, 0.9f));
            if (phase == Phase.Crouch || phase == Phase.Air) Disc(to, contactRadius, new Color(1f, 0.5f, 0f, 0.9f));
        }

        protected override void Interrupted()
        {
            if (phase == Phase.Crouch)
            {
                phase = Phase.Chase;
                Motor.LookAt = null;
                CooldownRemaining = Mathf.Max(CooldownRemaining, cooldown * 0.5f);
            }
        }

        protected override void WhileHeld(float dt)
        {
            if (phase != Phase.Air) return;

            // 気絶で倒れ込んだら、飛ぶのはやめて物理に任せる（起き上がったら Motor が位置を合わせ直す）。
            if (ragdoll != null && ragdoll.IsRagdoll)
            {
                phase = Phase.Chase;
                    Motor.Resume();
                return;
            }
            Fly(dt, false);
        }

    }
}
