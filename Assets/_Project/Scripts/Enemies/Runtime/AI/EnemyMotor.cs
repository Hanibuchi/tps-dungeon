using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の歩き（浮くものは飛び）。NavMeshAgent で <see cref="Destination"/> へ向かい、Animator の Speed を書く。
    /// どこへ行くか・どちらを向くかは EnemyBrain が決めて、ここに書き込む。
    ///
    /// 浮く敵は agent の baseOffset で <see cref="hoverHeight"/> だけ持ち上げる（足元がルートの原点なので見た目ごと浮く）。
    /// 浮いている間は不規則に上下し（<see cref="hoverBob"/>）、撃たれるとさっと上か下へ逃げる（<see cref="evadeHeight"/>）。狙いにくくするため。
    /// Brain が <see cref="Descend"/> を立てている間は <see cref="diveHeight"/> まで降りる（近接の敵が殴りに降りてくる）。
    /// スタン・気絶・ラグドールの間（ノックバックやラグドールが位置を直接動かす）と、
    /// 飛びつきなどで Brain が自分で動かしている間（<see cref="Suspend"/>）は agent に位置を書かせない。
    /// 動けるようになったら今の位置へ agent を合わせ直す。倒れたら agent を切る。
    /// NavMesh はフロアの生成の後に焼くので、それまでは agent を止めて待つ。
    /// 敵のルート（NavMeshAgent と同じ GameObject）に付ける。MonsterBuilder がプレハブに焼き込む。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("TPS Dungeon/Enemy Motor")]
    public sealed class EnemyMotor : MonoBehaviour
    {
        // MonsterBuilder（エディタ専用アセンブリ）が焼き込んだ Animator のパラメータ名。向こうを変えたらここも揃えること。
        private const string SpeedParam = "Speed";

        [SerializeField, Min(0.1f), Tooltip("移動の速さ（m/秒）。")]
        private float moveSpeed = 3f;

        [SerializeField, Min(1f), Tooltip("向きを変える速さ（度/秒）。")]
        private float turnSpeed = 540f;

        [SerializeField, Min(0f), Tooltip("地面から浮く高さ（m）。0 なら歩く。")]
        private float hoverHeight;

        [SerializeField, Min(0f), Tooltip("浮いている間の上下の揺れ幅（m、片側）。")]
        private float hoverBob = 0.4f;

        [SerializeField, Min(0f), Tooltip("Descend の間に降りる高さ（m）。")]
        private float diveHeight = 0.4f;

        [SerializeField, Min(0f), Tooltip("撃たれたときに上か下へ逃げる幅（m）。")]
        private float evadeHeight = 0.7f;

        [SerializeField, Min(0.1f), Tooltip("浮く高さを変える速さ（m/秒）。")]
        private float climbSpeed = 3.5f;

        [SerializeField, Min(0.05f), Tooltip("目的地を付け直す間隔（秒）。")]
        private float repathInterval = 0.2f;

        private NavMeshAgent agent;
        private Animator animator;
        private EnemyHealth health;
        private EnemyDamageReaction reaction;
        private EnemyRagdoll ragdoll;
        private int speedHash;
        private float animationBlend;
        private float nextRepath;
        private float bobPhase;
        private float bobTime;
        private float currentHover;
        private float evadeOffset;
        private float evadeRemaining;
        private bool held;
        private bool suspended;

        /// <summary>向かう先。null なら止まる。</summary>
        public Vector3? Destination { get; set; }

        /// <summary>向かう先にこの距離まで近づいたら止まる（m）。</summary>
        public float StopDistance { get; set; } = 0.5f;

        /// <summary>移動の速さに掛ける倍率（導火中に遅くするなど）。</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>向く先。null なら進む向きを向く。</summary>
        public Vector3? LookAt { get; set; }

        public float MoveSpeed => moveSpeed;
        public float HoverHeight => hoverHeight;

        /// <summary>浮く敵を <see cref="diveHeight"/> まで降ろす（殴りに降りる間）。歩く敵には効かない。</summary>
        public bool Descend { get; set; }

        /// <summary>浮く敵が降りきっている（歩く敵は常に真）。</summary>
        public bool IsDown => hoverHeight <= 0f || currentHover <= diveHeight + 0.2f;

        /// <summary>NavMesh に乗っていて、agent で動ける。</summary>
        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh && !held && !suspended;

        /// <summary>今の位置に近い NavMesh 上の点（無ければそのまま）。</summary>
        public static Vector3 OnNavMesh(Vector3 position, float radius = 2f) =>
            NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas) ? hit.position : position;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<EnemyHealth>();
            reaction = GetComponent<EnemyDamageReaction>();
            ragdoll = GetComponent<EnemyRagdoll>();
            speedHash = Animator.StringToHash(SpeedParam);
            bobPhase = Random.value * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            animationBlend = 0f;
            nextRepath = 0f;
            held = false;
            suspended = false;
            if (health != null)
            {
                health.Died += OnDied;
                health.Damaged += OnDamaged;
            }
            currentHover = hoverHeight;
            evadeRemaining = 0f;
            Descend = false;
            TryPlaceAgent(transform.position);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
                health.Damaged -= OnDamaged;
            }
            if (agent != null) agent.enabled = false;
        }

        private void OnDied(EnemyHealth _)
        {
            enabled = false;
        }

        /// <summary>撃たれたら、今の揺れと逆の向き（上に居れば下、下に居れば上）へさっと逃げる。</summary>
        private void OnDamaged(EnemyHealth _, DamageInfo __, int ___)
        {
            if (hoverHeight <= 0f || Descend) return;
            evadeOffset = (Bob() > 0f ? -1f : 1f) * evadeHeight;
            evadeRemaining = 0.8f;
        }

        /// <summary>不規則な上下の揺れ（-hoverBob〜+hoverBob）。2 つの周期を重ねて、読まれにくくする。</summary>
        private float Bob() => hoverBob * (0.7f * Mathf.Sin(bobTime * 1.6f + bobPhase) + 0.3f * Mathf.Sin(bobTime * 3.7f + bobPhase * 2f));

        /// <summary>Brain が自分で位置を動かす間、agent を止める（飛びつきの間など）。</summary>
        public void Suspend()
        {
            if (suspended) return;
            suspended = true;
            Release();
        }

        /// <summary><see cref="Suspend"/> を解いて、今の位置（の近くの NavMesh 上）に agent を合わせ直す。</summary>
        public void Resume()
        {
            if (!suspended) return;
            suspended = false;
            if (!held) Catch();
        }

        /// <summary>向きを target の方へ、回る速さの範囲で寄せる。</summary>
        public void TurnTowards(Vector3 target, float deltaTime)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return;
            Quaternion want = Quaternion.LookRotation(to, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * deltaTime);
        }

        /// <summary>今 target の方を向いているか（水平の角度が maxAngle 度以内）。</summary>
        public bool IsFacing(Vector3 target, float maxAngle)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            return to.sqrMagnitude < 1e-4f || Vector3.Angle(transform.forward, to) <= maxAngle;
        }

        private bool TryPlaceAgent(Vector3 near)
        {
            if (agent == null) return false;
            if (agent.enabled && agent.isOnNavMesh) return true;
            if (!NavMesh.SamplePosition(near, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;

            agent.enabled = true;
            agent.baseOffset = hoverHeight > 0f ? currentHover : 0f;
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        private void Update()
        {
            bool nowHeld = (reaction != null && !reaction.CanAct) || (ragdoll != null && ragdoll.IsRagdoll);
            if (nowHeld != held)
            {
                held = nowHeld;
                if (held) Release();
                else if (!suspended) Catch();
            }

            if (held || suspended)
            {
                WriteAnimator(0f);
                return;
            }

            // NavMesh がまだ無い（フロアの生成待ち）。
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                TryPlaceAgent(transform.position);
                WriteAnimator(0f);
                return;
            }

            if (hoverHeight > 0f) UpdateHover(Time.deltaTime);

            agent.speed = moveSpeed * Mathf.Max(0f, SpeedScale);
            agent.angularSpeed = turnSpeed;
            agent.stoppingDistance = StopDistance;
            agent.updateRotation = !LookAt.HasValue;
            if (LookAt.HasValue) TurnTowards(LookAt.Value, Time.deltaTime);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                if (Destination.HasValue && Flat(Destination.Value - transform.position).magnitude > StopDistance)
                {
                    agent.isStopped = false;
                    agent.SetDestination(OnNavMesh(Destination.Value));
                }
                else
                {
                    agent.isStopped = true;
                    if (agent.hasPath) agent.ResetPath();
                }
            }

            WriteAnimator(Flat(agent.velocity).magnitude);
        }

        /// <summary>浮く高さを、狙う高さ（降りる・揺れ・逃げ）へ一定の速さで寄せる。</summary>
        private void UpdateHover(float dt)
        {
            bobTime += dt;
            if (evadeRemaining > 0f)
            {
                evadeRemaining -= dt;
                if (evadeRemaining <= 0f) evadeOffset = 0f;
            }

            float want = Descend ? diveHeight : hoverHeight + Bob() + evadeOffset;
            want = Mathf.Max(0.1f, want);
            // 逃げるときは速く動く。
            float speed = evadeRemaining > 0f ? climbSpeed * 2f : climbSpeed;
            currentHover = Mathf.MoveTowards(currentHover, want, speed * dt);
            agent.baseOffset = currentHover;
        }

        /// <summary>agent に位置を書かせないようにする（外から位置を動かす間）。</summary>
        private void Release()
        {
            if (agent == null || !agent.enabled) return;
            agent.updatePosition = false;
            agent.updateRotation = false;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.velocity = Vector3.zero;
        }

        /// <summary>外から動かされた今の位置へ agent を合わせ直し、また位置を書かせる。</summary>
        private void Catch()
        {
            if (agent == null) return;

            // updatePosition を戻すと transform が agent の（止めた時の）位置へ引き戻されるので、先に今の位置を取っておく。
            Vector3 here = transform.position;
            agent.updatePosition = true;
            nextRepath = 0f;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                TryPlaceAgent(here);
                return;
            }
            agent.Warp(OnNavMesh(here, 4f));
        }

        private void WriteAnimator(float speed)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animationBlend = Mathf.Lerp(animationBlend, speed, Time.deltaTime * 10f);
            if (animationBlend < 0.01f) animationBlend = 0f;
            animator.SetFloat(speedHash, animationBlend);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
