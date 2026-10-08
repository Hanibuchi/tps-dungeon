using System;
using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Items;
using TpsDungeon.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティー（最大 <see cref="MaxMembers"/> 人、先頭を含む）。先頭が操作しているキャラで、残りは列になってついてくる。
    /// プレイヤーと仲間に違いは無く、並べ替えで先頭に来た人を操作する。
    /// - 並び順（<see cref="Order"/>）を持ち、<see cref="PartyRoster"/> に写して HUD や画面に知らせる。
    /// - 共有のバッグ（<see cref="Bag"/>）を持ち、全員の PlayerInventory に同じものを渡す。
    /// - 先頭が通った跡（<see cref="Trail"/>）を残し、i 番目の人には跡に沿って i × 間隔だけ後ろの点を歩かせる。
    /// - 先頭が替わったら、操作（CharacterMotor・攻撃・インタラクト・ホットバーの入力）と、カメラの追う先を新しい先頭へ移す。
    ///   カメラの向きは引き継ぐので、替えた瞬間に視点は跳ねない。
    /// パーティーの操作台（PlayerInput・CharacterInput・PartyProgression・UI と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("TPS Dungeon/Party")]
    public sealed class Party : MonoBehaviour
    {
        public const int MaxMembers = 12;

        [SerializeField, Tooltip("最初の並び。先頭が操作するキャラ。")]
        private List<PartyMember> members = new List<PartyMember>();

        [SerializeField, Min(0), Tooltip("共有のバッグの枠の数。")]
        private int bagCapacity = 12;

        [SerializeField, Min(0.5f), Tooltip("列で前の人との間（m）。")]
        private float spacing = 1.6f;

        [SerializeField, Tooltip("移動の入力。未設定ならこの GameObject から探す。")]
        private CharacterInput input;

        [SerializeField, Tooltip("先頭を追うカメラ。未設定ならシーンから探す。")]
        private CinemachineCamera followCamera;

        [SerializeField, Min(1f), Tooltip("先頭が 1 フレームでこれより大きく動いたら（階段・ワープ）、跡を作り直して全員を後ろへ呼び寄せる（m）。")]
        private float teleportDistance = 6f;

        private PartyOrder<PartyMember> order;
        private TrailPath trail;
        private Inventory bag;
        private Vector3 lastLeaderPosition;

        /// <summary>今のパーティー。シーンに 1 つ。</summary>
        public static Party Current { get; private set; }

        public PartyOrder<PartyMember> Order => EnsureOrder();

        public IReadOnlyList<PartyMember> Members => Order.Members;

        public PartyMember Leader => Order.Leader;

        /// <summary>共有のバッグ。</summary>
        public Inventory Bag
        {
            get
            {
                if (bag == null) bag = new Inventory(0, bagCapacity);
                return bag;
            }
        }

        /// <summary>先頭が通った跡。</summary>
        public TrailPath Trail => trail ??= new TrailPath(0.4f, spacing * (MaxMembers + 2));

        /// <summary>列で前の人との間（m）。</summary>
        public float Spacing => spacing;

        public CharacterInput Input => input;

        /// <summary>並び（人数・順番）が変わった。</summary>
        public event Action<Party> Changed;

        /// <summary>全員を先頭の後ろへ呼び寄せた（始まり・ワープ・先頭の交代）。仲間の歩きが跡を拾い直す。</summary>
        public event Action<Party> Regrouped;

        private void Awake()
        {
            if (input == null) input = GetComponent<CharacterInput>();
            EnsureOrder();
        }

        private void OnEnable()
        {
            Current = this;
            ApplyAll();
            FloorNavMesh.Baked += OnNavMeshBaked;
        }

        private void OnDisable()
        {
            FloorNavMesh.Baked -= OnNavMeshBaked;
            if (Current == this) Current = null;
            PartyRoster.Clear();
        }

        private void Start()
        {
            Regroup();
        }

        private void LateUpdate()
        {
            PartyMember leader = Leader;
            if (leader == null) return;

            Vector3 position = leader.transform.position;
            if (Vector3.Distance(position, lastLeaderPosition) > teleportDistance)
            {
                Regroup();
                return;
            }

            Trail.Record(position);
            lastLeaderPosition = position;
        }

        private PartyOrder<PartyMember> EnsureOrder()
        {
            if (order != null) return order;

            order = new PartyOrder<PartyMember>(MaxMembers);
            foreach (PartyMember member in members.Where(m => m != null).Distinct().Take(MaxMembers)) order.Add(member);
            order.Changed += OnOrderChanged;
            order.LeaderChanged += OnLeaderChanged;
            return order;
        }

        /// <summary>
        /// 並び替え（from 番目を to 番目へ）。to を 0 にすればその人が先頭になって操作が移る。動いたら true。
        /// </summary>
        public bool Move(int from, int to) => Order.Move(from, to);

        /// <summary>列の最後に加える。満員なら false。</summary>
        public bool Add(PartyMember member)
        {
            if (member == null || !Order.Add(member)) return false;
            if (!members.Contains(member)) members.Add(member);
            return true;
        }

        /// <summary>パーティーから外す（キャラは消さない）。居なければ false。</summary>
        public bool Remove(PartyMember member)
        {
            if (!Order.Remove(member)) return false;
            members.Remove(member);
            member.Leave();
            return true;
        }

        public int IndexOf(PartyMember member) => Order.IndexOf(member);

        /// <summary>index 番目の人が立つ列の位置（跡に沿って index × 間隔だけ後ろ）。</summary>
        public Vector3 FormationPoint(int index) => Trail.PointAt(Mathf.Max(0, index) * spacing);

        /// <summary>
        /// 先頭の後ろに跡をまっすぐ作り直し、後ろの全員をその列の位置へ跳ばす。始まりと、先頭が跳んだときに使う。
        /// </summary>
        public void Regroup()
        {
            PartyMember leader = Leader;
            if (leader == null) return;

            Transform t = leader.transform;
            lastLeaderPosition = t.position;
            Trail.Reset(t.position, -t.forward);
            for (int i = 1; i < Order.Count; i++) Order[i].PlaceAt(FormationPoint(i), t.rotation);
            Regrouped?.Invoke(this);
        }

        private void OnNavMeshBaked() => Regroup();

        private void OnOrderChanged(PartyOrder<PartyMember> _)
        {
            for (int i = 0; i < Order.Count; i++) Order[i].Join(this, i);
            PartyRoster.Set(Order.Members.Select(m => m.gameObject));
            Changed?.Invoke(this);
        }

        private void OnLeaderChanged(PartyMember before, PartyMember after)
        {
            // 前の先頭のカメラの向きを新しい先頭へ渡す。
            if (before != null && after != null && before.Motor != null && after.Motor != null)
            {
                after.Motor.CameraYaw = before.Motor.CameraYaw;
                after.Motor.CameraPitch = before.Motor.CameraPitch;
            }

            if (before != null && Order.Contains(before)) before.SetLeader(false, input);
            if (after != null) after.SetLeader(true, input);
            RetargetCamera();

            // 新しい先頭の後ろに跡を作り直す。後ろの人はそこから歩いて並び直す（跳ばさない）。
            if (after != null)
            {
                lastLeaderPosition = after.transform.position;
                Trail.Reset(after.transform.position, -after.transform.forward);
                Regrouped?.Invoke(this);
            }
        }

        /// <summary>全員に並びと役（先頭か）を当て直す。</summary>
        private void ApplyAll()
        {
            for (int i = 0; i < Order.Count; i++)
            {
                PartyMember member = Order[i];
                member.Join(this, i);
                member.SetLeader(i == 0, input);
            }

            PartyRoster.Set(Order.Members.Select(m => m.gameObject));
            RetargetCamera();
        }

        private void RetargetCamera()
        {
            PartyMember leader = Leader;
            if (leader == null) return;

            if (followCamera == null) followCamera = FindAnyObjectByType<CinemachineCamera>();
            Transform target = leader.CameraTarget;
            if (followCamera != null && target != null) followCamera.Target.TrackingTarget = target;
        }

        /// <summary>position の近くの NavMesh 上の点。見つからなければ position のまま。</summary>
        public static Vector3 OnNavMesh(Vector3 position, float radius = 2f)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas) ? hit.position : position;
        }

        // ドメインリロードを切っているので、再生のたびに前回のパーティーを捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
