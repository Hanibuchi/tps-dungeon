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
    /// - 倒れた人（<see cref="PartyMember.IsDown"/>）は列の最後へ回す（倒れた順）。先頭が倒れれば、生きている一番前の人に操作が移る。
    ///   全員が倒れたときだけは最後の人を先頭のままにする（全滅の後もカメラがその人を映すように）。生きている人は必ず倒れた人より前に並ぶ。
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

        /// <summary>倒れていない人の数。並びの先頭からこの人数が生きている人。</summary>
        public int AliveCount => Order.Members.Count(m => m != null && !m.IsDown);

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
        /// 倒れた人を生きている人より前へ、生きている人を倒れた人より後ろへは動かせない。
        /// </summary>
        public bool Move(int from, int to)
        {
            PartyMember moving = Order[from];
            if (moving == null) return false;

            int alive = AliveCount;
            if (moving.IsDown ? to < alive : to >= alive) return false;
            return Order.Move(from, to);
        }

        /// <summary>生きている人の最後（倒れた人の前）に加える。満員なら false。</summary>
        public bool Add(PartyMember member)
        {
            if (member == null || !Order.Add(member)) return false;
            if (!members.Contains(member)) members.Add(member);
            if (!member.IsDown) Order.Move(Order.Count - 1, AliveCount - 1);
            return true;
        }

        /// <summary>倒れた・起きたと PartyMember から知らせが来た。倒れたなら列の最後へ回す（全員倒れたら動かさない）。</summary>
        internal void OnMemberDownChanged(PartyMember member)
        {
            int index = Order.IndexOf(member);
            if (index < 0 || !member.IsDown || AliveCount == 0) return;
            Order.Move(index, Order.Count - 1);
        }

        /// <summary>倒れた人をこの階に置いていく（別のシーンへ移る直前に）。パーティーには残し、次のシーンには出さない。転生で <see cref="ReviveAll"/> が出す。</summary>
        public void LeaveDownedBehind()
        {
            foreach (PartyMember member in Order.Members)
            {
                if (member != null && member.IsDown) member.gameObject.SetActive(false);
            }
        }

        /// <summary>全員を全快にして起こす（転生のとき）。置いていった人も出し、役（先頭か）を当て直す。</summary>
        public void ReviveAll()
        {
            foreach (PartyMember member in Order.Members)
            {
                if (member == null) continue;
                if (!member.gameObject.activeSelf) member.gameObject.SetActive(true);
                member.Revive();
            }
            ApplyAll();
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

        /// <summary>
        /// index 番目の人が立つ列の位置（跡に沿って index × 間隔だけ後ろ）。
        /// 跡が足りない（作り直した直後で後ろが壁）ときは、跡の端の左右に互い違いに並べる。
        /// </summary>
        public Vector3 FormationPoint(int index)
        {
            float distance = Mathf.Max(0, index) * spacing;
            Vector3 point = Trail.PointAt(distance);
            float length = Trail.Length;
            if (index <= 0 || distance <= length + 0.01f) return point;

            // 跡の端から何人目にはみ出したか（1 始まり）。左右へ互い違いに 1 間隔ずつ開く。
            int over = Mathf.CeilToInt((distance - length) / spacing);
            Vector3 along = Trail.TailDirection ?? (Leader != null ? -Leader.transform.forward : Vector3.back);
            Vector3 side = Vector3.Cross(Vector3.up, along).normalized;
            float offset = ((over + 1) / 2) * spacing * (over % 2 == 1 ? 1f : -1f);
            Vector3 wanted = point + side * offset;
            return NavMesh.Raycast(OnNavMesh(point), wanted, out NavMeshHit hit, NavMesh.AllAreas) ? hit.position : wanted;
        }

        /// <summary>
        /// 先頭を at の位置と向き（水平の回転だけ）へ移し、カメラも同じ向きにする。シーンに着いたときのスポーン地点に使う。
        /// 後ろの人は、先頭が跳んだのを LateUpdate が見て並び直す。
        /// </summary>
        public void PlaceLeaderAt(Transform at)
        {
            PartyMember leader = Leader;
            if (leader == null || at == null) return;

            float yaw = at.eulerAngles.y;
            leader.PlaceAt(at.position, Quaternion.Euler(0f, yaw, 0f));
            if (leader.Motor != null)
            {
                leader.Motor.CameraYaw = yaw;
                leader.Motor.CameraPitch = 0f;
            }
        }

        /// <summary>
        /// 先頭の後ろに跡をまっすぐ作り直し、後ろの全員をその列の位置へ跳ばす。始まりと、先頭が跳んだときに使う。
        /// 真後ろに少しでも余地があれば真後ろに並べる（階段の途中から始まるときなど、後ろが狭くても前に回り込ませない）。
        /// 全員が入りきらなければ、間隔を詰めて真後ろに収める。歩き出せば元の間隔に戻る。
        /// </summary>
        public void Regroup()
        {
            PartyMember leader = Leader;
            if (leader == null) return;

            Transform t = leader.transform;
            ResetTrailBehind(leader, preferStraightBack: true);

            // NavMesh がまだ無い（フロアの生成と焼き上がりを待っている）間は並べない。焼けたら Baked でもう一度呼ばれる。
            if (!NavMesh.SamplePosition(t.position, out NavMeshHit _, 2f, NavMesh.AllAreas)) return;

            // 倒れた人（列の後ろ）は倒れた所に置いたまま。
            int alive = AliveCount;
            float step = PackedSpacing(spacing, Trail.Length, alive - 1);
            for (int i = 1; i < alive; i++)
            {
                Vector3 at = step >= spacing ? FormationPoint(i) : BehindOnGround(t, step * i);
                Order[i].PlaceAt(at, t.rotation);
            }
            Regrouped?.Invoke(this);
        }

        /// <summary>
        /// leader の真後ろ distance（水平）の所の、足元の地面（段や坂）の上の NavMesh の点。
        /// 階段の途中のように後ろが上り下りしていても、その段の上に乗せる（同じ高さで一番近い NavMesh を探すと、坂の下の床などに寄ってしまう）。
        /// </summary>
        private Vector3 BehindOnGround(Transform leader, float distance)
        {
            Vector3 back = -leader.forward;
            back.y = 0f;
            Vector3 point = leader.position + back.normalized * distance;
            if (Physics.Raycast(point + Vector3.up * GroundProbeHeight, Vector3.down, out RaycastHit ground, GroundProbeHeight * 2f, GroundMask, QueryTriggerInteraction.Ignore)
                && NavMesh.SamplePosition(ground.point, out NavMeshHit onGround, 0.6f, NavMesh.AllAreas))
            {
                return onGround.position;
            }
            return OnNavMesh(point);
        }

        // 真後ろの地面を探すとき、その点のどれだけ上からレイを落とすか。急な坂の段 1 つぶんより高く、天井より低く。
        private const float GroundProbeHeight = 2.5f;

        /// <summary>地面を探すレイが当たる層。パーティーの体（Ally）や壊せる草花（Breakable）は素通しする。</summary>
        private static int GroundMask
        {
            get
            {
                int mask = ~0;
                foreach (string layer in new[] { "Ally", "Breakable" })
                {
                    int index = LayerMask.NameToLayer(layer);
                    if (index >= 0) mask &= ~(1 << index);
                }
                return mask;
            }
        }

        /// <summary>
        /// 長さ length の跡に followers 人を並べるときの間隔。普段の間隔（spacing）で入りきらなければ、入りきるまで詰める。
        /// </summary>
        public static float PackedSpacing(float spacing, float length, int followers)
        {
            if (followers <= 0) return spacing;
            return Mathf.Min(spacing, Mathf.Max(0f, length) / followers);
        }

        private void OnNavMeshBaked() => Regroup();

        /// <summary>
        /// leader の後ろに跡を作り直す。真後ろが壁で詰まっていれば、NavMesh の上で一番開けている向きを使い、
        /// 跡は壁に当たる所までにする（後ろの人が壁の向こうの部屋へ回り込まないように）。
        /// </summary>
        private void ResetTrailBehind(PartyMember leader, bool preferStraightBack = false)
        {
            Transform t = leader.transform;
            lastLeaderPosition = t.position;
            float wanted = spacing * (Mathf.Max(1, AliveCount) - 1) + spacing;

            Vector3 origin = OnNavMesh(t.position);

            // 並べ直し（始まり・跳んだとき）では、真後ろに 1 人ぶんの半分でも余地があれば真後ろを使う。
            if (preferStraightBack)
            {
                float back = FreeLength(origin, -t.forward, wanted);
                if (back >= spacing * 0.5f)
                {
                    Trail.Reset(t.position, -t.forward, back);
                    return;
                }
            }

            // 真後ろから左右へ 45 度ずつ向きを変えて探し、数人ぶん並べる長さがある最初の向きを使う（後ろに近い向きほど先）。
            // どの向きも短ければ、一番長い向き。
            float enough = Mathf.Min(wanted, spacing * 2.5f);
            Vector3 best = -t.forward;
            float bestLength = -1f;
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * -t.forward;
                float length = FreeLength(origin, direction, wanted);
                if (length > bestLength + 0.1f)
                {
                    best = direction;
                    bestLength = length;
                }

                if (length >= enough) break;
            }

            Trail.Reset(t.position, best, bestLength);
        }

        /// <summary>origin から direction へ、NavMesh の端（壁）に当たるまでの長さ（最大 max）。NavMesh が無ければ max。</summary>
        private static float FreeLength(Vector3 origin, Vector3 direction, float max)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return 0f;
            Vector3 end = origin + direction.normalized * max;
            if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 1f, NavMesh.AllAreas)) return max;
            if (!NavMesh.Raycast(start.position, end, out NavMeshHit hit, NavMesh.AllAreas)) return max;
            // 坂や段の上では高さも変わるので、水平の距離で測る（並べるのも水平に測った所）。
            Vector3 along = hit.position - start.position;
            along.y = 0f;
            return Mathf.Max(0f, along.magnitude - 0.4f);
        }

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
                ResetTrailBehind(after);
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
