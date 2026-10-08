using TpsDungeon.Combat;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティーの後ろの人の歩き。NavMeshAgent で、列の位置（Party.FormationPoint、先頭の通った跡に沿って並び順 × 間隔だけ後ろ）へ歩く。
    /// 近接で戦う間は FollowerBrain が <see cref="ChaseTarget"/> に敵の位置を入れ、列を離れてそこへ寄る。
    /// 歩きのアニメーション（Speed / MotionSpeed / Grounded）は agent の速さから書く（先頭なら CharacterMotor が書いている分）。
    /// 先頭から大きく離れたり NavMesh の外に出たりしたら、列の位置へ跳ばして呼び戻す。
    /// 突き（MeleeAttacker の走る段）で走っている間は agent に位置を書かせず、走り終えたら今の位置に合わせ直す。
    /// NavMesh はフロアの生成の後に焼くので、それまでは agent を止めて待つ。
    /// キャラのルート（NavMeshAgent と同じ GameObject）に付ける。パーティーの先頭の間は PartyMember が止める。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("TPS Dungeon/Follower Locomotion")]
    public sealed class FollowerLocomotion : MonoBehaviour
    {
        private static readonly int AnimIdSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIdMotionSpeed = Animator.StringToHash("MotionSpeed");
        private static readonly int AnimIdGrounded = Animator.StringToHash("Grounded");
        private static readonly int AnimIdJump = Animator.StringToHash("Jump");
        private static readonly int AnimIdFreeFall = Animator.StringToHash("FreeFall");

        [SerializeField, Min(0.1f), Tooltip("歩く速さ（m/秒）。先頭の CharacterMotor の歩きに揃える。")]
        private float walkSpeed = 2f;

        [SerializeField, Min(0.1f), Tooltip("走る速さ（m/秒）。先頭の走りに追いつけるよう少し速めにする。")]
        private float runSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("目標までこれより遠ければ走る（m）。")]
        private float runDistance = 2.5f;

        [SerializeField, Min(0f), Tooltip("目標までこれより近ければ止まる（m）。")]
        private float arriveDistance = 0.35f;

        [SerializeField, Min(1f), Tooltip("先頭からこれより離れたら、列の位置へ跳ばして呼び戻す（m）。")]
        private float catchUpDistance = 22f;

        [SerializeField, Min(0.5f), Tooltip("道が見つからない・先へ進めないまま、これだけ経ったら列の位置へ跳ばす（秒）。")]
        private float stuckTime = 3f;

        [SerializeField, Min(0.05f), Tooltip("目的地を付け直す間隔（秒）。")]
        private float repathInterval = 0.2f;

        private NavMeshAgent agent;
        private Animator animator;
        private PartyMember member;
        private MeleeAttacker melee;
        private PlayerLocomotionSpeed speedMultiplier;
        private float nextRepath;
        private float stuckTimer;
        private bool wasLunging;
        private float animationBlend;

        /// <summary>列を離れて寄る先（近接で戦う間の敵の位置）。null なら列の位置へ歩く。</summary>
        public Vector3? ChaseTarget { get; set; }

        /// <summary>寄る先にこの距離まで近づいたら止まる（m）。</summary>
        public float ChaseStopDistance { get; set; } = 1f;

        /// <summary>体の向きを歩く向きに合わせるか。狙っている間は FollowerBrain が偽にして、攻撃の役に向きを任せる。</summary>
        public bool FaceMovement { get; set; } = true;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            member = GetComponent<PartyMember>();
            melee = GetComponent<MeleeAttacker>();
            speedMultiplier = GetComponent<PlayerLocomotionSpeed>();
        }

        private void OnEnable()
        {
            stuckTimer = 0f;
            nextRepath = 0f;
            wasLunging = false;
            animationBlend = 0f;
            TryPlaceAgent(transform.position);
        }

        private void OnDisable()
        {
            if (agent != null) agent.enabled = false;
            ChaseTarget = null;
            FaceMovement = true;
        }

        /// <summary>position（の近くの NavMesh 上）へ跳ばす。</summary>
        public void Warp(Vector3 position, Quaternion rotation)
        {
            Vector3 onMesh = Party.OnNavMesh(position, 4f);
            transform.SetPositionAndRotation(onMesh, rotation);
            Physics.SyncTransforms();
            if (agent.enabled && agent.isOnNavMesh) agent.Warp(onMesh);
            else TryPlaceAgent(onMesh);
            stuckTimer = 0f;
            nextRepath = 0f;
        }

        private bool TryPlaceAgent(Vector3 near)
        {
            if (agent == null) return false;
            if (agent.enabled && agent.isOnNavMesh) return true;
            if (!NavMesh.SamplePosition(near, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return false;

            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        private void Update()
        {
            Party party = member != null ? member.Party : null;
            if (party == null || party.Leader == null)
            {
                WriteAnimator(0f);
                return;
            }

            // NavMesh がまだ無い（フロアの生成待ち）。
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                if (!TryPlaceAgent(transform.position)) WriteAnimator(0f);
                return;
            }

            if (HandleLunge()) return;

            Vector3 leader = party.Leader.transform.position;
            Vector3 formation = party.FormationPoint(member.Index);
            if (Vector3.Distance(transform.position, leader) > catchUpDistance)
            {
                Warp(formation, party.Leader.transform.rotation);
                return;
            }

            Vector3 target = ChaseTarget ?? formation;
            float stop = ChaseTarget.HasValue ? ChaseStopDistance : arriveDistance;
            float distance = Flat(target - transform.position).magnitude;

            float multiplier = speedMultiplier != null ? speedMultiplier.Multiplier : 1f;
            agent.speed = (distance > runDistance ? runSpeed : walkSpeed) * multiplier;
            agent.stoppingDistance = stop;
            agent.updateRotation = FaceMovement;

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                if (distance > stop) agent.SetDestination(Party.OnNavMesh(target));
                else if (agent.hasPath) agent.ResetPath();
            }

            UpdateStuck(distance > stop + 0.5f, party);
            WriteAnimator(Flat(agent.velocity).magnitude);
        }

        /// <summary>突きで走っている間は agent に位置を書かせない。走り終えたら今の位置に合わせ直す。走っている間は真。</summary>
        private bool HandleLunge()
        {
            bool lunging = melee != null && melee.IsLunging;
            if (lunging)
            {
                if (!wasLunging)
                {
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    if (agent.hasPath) agent.ResetPath();
                }

                wasLunging = true;
                return true;
            }

            if (wasLunging)
            {
                wasLunging = false;
                agent.updatePosition = true;
                agent.Warp(Party.OnNavMesh(transform.position));
            }

            return false;
        }

        private void UpdateStuck(bool wantsToMove, Party party)
        {
            bool blocked = wantsToMove && (agent.pathStatus != NavMeshPathStatus.PathComplete || agent.velocity.sqrMagnitude < 0.01f) && !agent.pathPending;
            stuckTimer = blocked ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer < stuckTime) return;

            // 戦っていて寄れないだけなら列に戻るだけにし、列にも戻れないときに跳ばす。
            if (ChaseTarget.HasValue)
            {
                ChaseTarget = null;
                stuckTimer = 0f;
                return;
            }

            Warp(party.FormationPoint(member.Index), party.Leader.transform.rotation);
        }

        private void WriteAnimator(float speed)
        {
            if (animator == null) return;

            // CharacterMotor と同じく、Speed は目標の速さへなめらかに寄せる（歩き 2 m/秒・走り 6 m/秒で閾値を切ってある）。
            animationBlend = Mathf.Lerp(animationBlend, speed, Time.deltaTime * 10f);
            if (animationBlend < 0.01f) animationBlend = 0f;
            animator.SetFloat(AnimIdSpeed, animationBlend);
            animator.SetFloat(AnimIdMotionSpeed, 1f);
            animator.SetBool(AnimIdGrounded, true);
            animator.SetBool(AnimIdJump, false);
            animator.SetBool(AnimIdFreeFall, false);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
