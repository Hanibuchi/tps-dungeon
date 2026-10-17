using System;
using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Enemies;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Generation;
using TpsDungeon.Map.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 部屋に敵を出す。部屋のドアが初めて開けられたとき、その両側の部屋のうちまだ出していない普通の部屋に、
    /// 層の表（FloorBootstrap.EnemySpawns）から決めたグループ（RoomEnemyRoller）を出す。グループごとに部屋の中のランダムな 1 点の周りに出す。
    /// 中身は階のシードと部屋の番号から決まるので、開ける順に関係なく同じ部屋は同じになる。
    ///
    /// モンスターハウス（普通の部屋が表の確率でなる）のドアを外から開けると、パーティー全員をその部屋の中へ跳ばし、
    /// 部屋のドアを全部閉ざして閉める（ボス部屋の奥の扉と同じく「閉ざされている」）。中の敵を全部倒して releaseDelay 秒（既定 1 秒）経つと、閉ざしたドアを開けられるようにする。
    /// 閉じ込めたとき・鍵が開いたときに、層の表の音を鳴らす。モンスターハウスの予算の上限は表の倍率を掛けたもの。
    /// 残りは倒れた知らせ（EnemyHealth.Died）では数えず、閉じ込めている間に毎フレーム数え直す。自爆する敵は Died を出さずに消え、
    /// ドアが閉まりきる前に部屋の外へ出た敵は中から倒せないので、消えた・倒れた・部屋の外に居る敵は残りに入れない。
    /// FloorBuilder と同じ GameObject に付ける（GameFlow が FloorNavMesh と一緒に付ける）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FloorBuilder))]
    [AddComponentMenu("TPS Dungeon/Room Enemy Spawner")]
    public sealed class RoomEnemySpawner : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("敵を出す点を、部屋の壁からこれだけ内側に取る（m）。")]
        private float wallMargin = 1f;

        [SerializeField, Min(0f), Tooltip("モンスターハウスで、パーティーからこれより近い所には敵を出さない（m）。")]
        private float partyClearance = 3f;

        [SerializeField, Min(0.5f), Tooltip("閉じ込めるとき、先頭をドアからこれだけ部屋の中に入った所に立たせる（m）。")]
        private float trapDepth = 1.8f;

        [SerializeField, Min(0f), Tooltip("モンスターハウスの最後の敵が居なくなってから、ドアの鍵を開ける（音を鳴らす）までの間（秒）。倒した瞬間の音と重ねないように。")]
        private float releaseDelay = 1f;

        private FloorBuilder builder;
        private FloorBootstrap bootstrap;
        private EnemySpawnTable table;
        private EnemySpawnRules rules;
        private FloorLayout layout;
        private Transform enemyRoot;

        private readonly HashSet<int> spawnedRooms = new HashSet<int>();
        private HashSet<int> monsterHouses = new HashSet<int>();
        // 部屋ごとの、その部屋に出した敵。
        private readonly Dictionary<int, List<EnemyHealth>> spawnedEnemies = new Dictionary<int, List<EnemyHealth>>();
        // 閉じ込めている部屋と、閉ざしたドア。
        private readonly Dictionary<int, List<Door>> sealedDoors = new Dictionary<int, List<Door>>();
        // 閉じ込めている部屋の、敵が居なくなった時刻。
        private readonly Dictionary<int, float> clearedAt = new Dictionary<int, float>();

        /// <summary>モンスターハウスに閉じ込めた（部屋の番号）。</summary>
        public event Action<int> Trapped;

        /// <summary>モンスターハウスの敵を全部倒して、閉ざしたドアを開けられるようにした（部屋の番号）。</summary>
        public event Action<int> Released;

        /// <summary>この階のモンスターハウスの部屋の番号。</summary>
        public IReadOnlyCollection<int> MonsterHouses => monsterHouses;

        /// <summary>閉じ込められている部屋があるか。</summary>
        public bool IsTrapped => sealedDoors.Count > 0;

        /// <summary>その部屋に出した敵のうち、消えておらず、倒れておらず、まだ部屋の中に居る数。</summary>
        public int AliveIn(int room)
        {
            if (layout == null || !spawnedEnemies.TryGetValue(room, out var enemies)) return 0;
            var bounds = layout.RoomAt(room).Bounds;
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && bounds.Contains(builder.WorldToCell(enemy.transform.position))) count++;
            }
            return count;
        }

        /// <summary>閉じ込めている部屋の敵が居なくなったら、releaseDelay 秒待ってから鍵を開ける。</summary>
        private void Update()
        {
            if (sealedDoors.Count == 0) return;
            List<int> ready = null;
            foreach (int room in sealedDoors.Keys)
            {
                if (AliveIn(room) > 0)
                {
                    clearedAt.Remove(room);
                    continue;
                }
                if (!clearedAt.TryGetValue(room, out float at)) clearedAt[room] = at = Time.time;
                if (Time.time - at >= releaseDelay) (ready ??= new List<int>()).Add(room);
            }
            if (ready == null) return;
            foreach (int room in ready) Release(room);
        }

        private void Awake()
        {
            builder = GetComponent<FloorBuilder>();
            bootstrap = GetComponent<FloorBootstrap>();
        }

        private void OnEnable()
        {
            builder.Built += OnBuilt;
            builder.DoorOpened += OnDoorOpened;
            // このコンポーネントより先に生成が済んでいたら、ここで支度する。
            if (builder.CurrentLayout != null) OnBuilt(builder.CurrentLayout);
        }

        private void OnDisable()
        {
            builder.Built -= OnBuilt;
            builder.DoorOpened -= OnDoorOpened;
        }

        /// <summary>階を作り直したら、出した部屋の記録を捨ててモンスターハウスを決め直す（敵は生成物と一緒に消えている）。</summary>
        private void OnBuilt(FloorLayout built)
        {
            layout = built;
            table = bootstrap != null ? bootstrap.EnemySpawns : null;
            rules = table != null ? table.BuildRules() : null;
            spawnedRooms.Clear();
            spawnedEnemies.Clear();
            sealedDoors.Clear();
            clearedAt.Clear();
            enemyRoot = null;
            monsterHouses = RoomEnemyRoller.PickMonsterHouses(rules, layout);
            if (monsterHouses.Count > 0) Debug.Log($"[Dungeon] モンスターハウス: 部屋 {string.Join(", ", monsterHouses)}", this);
        }

        private void OnDoorOpened(Door door, RoomEdge edge)
        {
            if (rules == null || layout == null) return;
            foreach (int room in new[] { edge.RoomA, edge.RoomB })
            {
                if (!spawnedRooms.Add(room)) continue;

                var groups = RoomEnemyRoller.RollRoom(rules, layout, room, monsterHouses.Contains(room));
                if (groups.Count == 0) continue;

                // 外から開けたモンスターハウスなら、先に全員を中へ入れてから（パーティーの近くを避けて）敵を出し、ドアを閉ざす。
                bool trap = monsterHouses.Contains(room) && !LeaderInside(room);
                if (trap) TrapParty(room, edge);
                Spawn(room, groups, trap);
                if (trap) SealRoom(room);
            }
        }

        private bool LeaderInside(int room)
        {
            var leader = Party.Current != null ? Party.Current.Leader : null;
            return leader != null && layout.RoomAt(room).Bounds.Contains(builder.WorldToCell(leader.transform.position));
        }

        // ---- 敵を出す ----

        private void Spawn(int roomIndex, List<EnemyGroup> groups, bool avoidParty)
        {
            var room = layout.RoomAt(roomIndex);
            var rng = new System.Random(RoomEnemyRoller.RoomSeed(layout.Seed, roomIndex, RoomEnemyRoller.PlacementSalt));
            if (!spawnedEnemies.TryGetValue(roomIndex, out var spawned)) spawnedEnemies[roomIndex] = spawned = new List<EnemyHealth>();
            foreach (var group in groups)
            {
                var prefab = table.PrefabOf(group.EnemyId);
                if (prefab == null) continue;

                Vector3 center = GroupCenter(room, rng, avoidParty);
                // 体数が多いほど広く散らす。部屋（1 セル）からはみ出さない程度に。
                float radius = Mathf.Min(0.6f + 0.35f * group.Count, builder.CellSize * 0.4f);
                for (int i = 0; i < group.Count; i++)
                {
                    Vector3 at = i == 0 ? center : AroundCenter(room, center, radius, rng);
                    var rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    var enemy = Instantiate(prefab, at, rotation, EnemyRoot());
                    enemy.name = $"{prefab.name}_Room{roomIndex}";
                    if (enemy.TryGetComponent(out EnemyHealth health)) spawned.Add(health);
                }
            }
        }

        /// <summary>生成物の下にまとめる（作り直したら一緒に消える）。NavMesh は焼き終えているので、これを含めて焼き直すことはない。</summary>
        private Transform EnemyRoot()
        {
            if (enemyRoot != null) return enemyRoot;
            enemyRoot = new GameObject("Enemies").transform;
            enemyRoot.SetParent(builder.GeneratedRoot != null ? builder.GeneratedRoot : transform, false);
            return enemyRoot;
        }

        /// <summary>グループを出す点。部屋の中（壁から wallMargin 内側）のランダムな NavMesh の上。閉じ込めたときはパーティーから離す。</summary>
        private Vector3 GroupCenter(RoomInstance room, System.Random rng, bool avoidParty)
        {
            Vector3 fallback = RoomPoint(room, 0.5f, 0.5f);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 p = RoomPoint(room, (float)rng.NextDouble(), (float)rng.NextDouble());
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) || !InRoom(room, hit.position)) continue;
                fallback = hit.position;
                if (!avoidParty || FarFromParty(hit.position)) return hit.position;
            }
            return Party.OnNavMesh(fallback);
        }

        /// <summary>center の周り radius 以内の、中心から NavMesh 上でまっすぐ行ける同じ部屋の点。見つからなければ center。</summary>
        private Vector3 AroundCenter(RoomInstance room, Vector3 center, float radius, System.Random rng)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                double angle = rng.NextDouble() * Math.PI * 2d;
                float distance = radius * Mathf.Sqrt((float)rng.NextDouble());
                Vector3 p = center + new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                if (!InRoom(room, hit.position) || NavMesh.Raycast(center, hit.position, out _, NavMesh.AllAreas)) continue;
                return hit.position;
            }
            return center;
        }

        /// <summary>部屋の床の、(u, v)（0〜1）の所のワールド座標。壁から wallMargin 内側に縮めてある。</summary>
        private Vector3 RoomPoint(RoomInstance room, float u, float v)
        {
            float cell = builder.CellSize;
            float width = room.Bounds.Width * cell, depth = room.Bounds.Height * cell;
            float marginX = Mathf.Min(wallMargin, width * 0.4f), marginZ = Mathf.Min(wallMargin, depth * 0.4f);
            var local = new Vector3(
                room.Bounds.X * cell + marginX + (width - marginX * 2f) * u,
                0f,
                room.Bounds.Y * cell + marginZ + (depth - marginZ * 2f) * v);
            return builder.transform.TransformPoint(local);
        }

        private bool InRoom(RoomInstance room, Vector3 world) => room.Bounds.Contains(builder.WorldToCell(world));

        private bool FarFromParty(Vector3 p)
        {
            var party = Party.Current;
            if (party == null) return true;
            foreach (var member in party.Members)
            {
                if (member == null) continue;
                Vector3 d = member.transform.position - p;
                d.y = 0f;
                if (d.magnitude < partyClearance) return false;
            }
            return true;
        }

        // ---- モンスターハウス ----

        /// <summary>
        /// パーティー全員を部屋の中へ跳ばす。先頭は開けたドアから trapDepth だけ入った所に部屋の奥を向いて、
        /// 仲間はその近くの同じ部屋の NavMesh の上へ。跡は先頭とドアの間だけにする（後ろの人がドアの外へ戻らないように）。
        /// </summary>
        private void TrapParty(int roomIndex, RoomEdge edge)
        {
            var party = Party.Current;
            if (party == null || party.Leader == null) return;

            var room = layout.RoomAt(roomIndex);
            var socket = edge.RoomA == roomIndex ? edge.SocketA : edge.SocketB;
            Vector3 doorway = builder.transform.TransformPoint(builder.BoundaryCenter(socket.Cell, socket.Facing));
            var step = socket.Facing.Offset();
            Vector3 inward = builder.transform.TransformDirection(new Vector3(-step.X, 0f, -step.Y)).normalized;

            float depth = Mathf.Min(trapDepth, builder.CellSize * 0.5f);
            Vector3 leaderAt = Party.OnNavMesh(doorway + inward * depth);
            if (!InRoom(room, leaderAt)) leaderAt = Party.OnNavMesh(builder.transform.TransformPoint(builder.CellCenter(socket.Cell)));

            var followers = FollowerPoints(room, leaderAt, party.Members.Count - 1);
            party.PlaceAllAt(leaderAt, Quaternion.LookRotation(inward, Vector3.up), followers, depth - 0.6f);
            Debug.Log($"[Dungeon] モンスターハウス（部屋 {roomIndex}）に閉じ込めた", this);
        }

        /// <summary>先頭の近くから順に、同じ部屋の NavMesh の上の点を count 個。部屋に 1.2 m 間隔の点を並べ、先頭に近い順に使う。</summary>
        private List<Vector3> FollowerPoints(RoomInstance room, Vector3 leaderAt, int count)
        {
            var candidates = new List<Vector3>();
            float cell = builder.CellSize;
            int nx = Mathf.Max(1, Mathf.FloorToInt(room.Bounds.Width * cell / 1.2f));
            int nz = Mathf.Max(1, Mathf.FloorToInt(room.Bounds.Height * cell / 1.2f));
            for (int x = 0; x < nx; x++)
            {
                for (int z = 0; z < nz; z++)
                {
                    Vector3 p = RoomPoint(room, (x + 0.5f) / nx, (z + 0.5f) / nz);
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) || !InRoom(room, hit.position)) continue;
                    if (Vector3.Distance(hit.position, leaderAt) < 0.8f) continue;
                    candidates.Add(hit.position);
                }
            }
            candidates.Sort((a, b) => (a - leaderAt).sqrMagnitude.CompareTo((b - leaderAt).sqrMagnitude));

            var points = new List<Vector3>(count);
            for (int i = 0; i < count; i++) points.Add(candidates.Count > 0 ? candidates[i % candidates.Count] : leaderAt);
            return points;
        }

        /// <summary>部屋のドアを全部閉ざして閉める。もともと閉ざされていた扉（ボス部屋の奥など）は開けるときに触らないよう控えない。</summary>
        private void SealRoom(int room)
        {
            var doors = new List<Door>();
            foreach (var door in builder.DoorsOf(room))
            {
                if (door.IsLocked) continue;
                door.Seal();
                doors.Add(door);
            }
            sealedDoors[room] = doors;
            PlaySound(table.TrappedSound);
            Trapped?.Invoke(room);
        }

        /// <summary>閉じ込め・鍵が開く音。パーティーは部屋の中に居るので、位置を付けずに鳴らす。</summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip != null) GameAudio.Instance?.PlaySe(clip, table.SoundVolume);
        }

        private void Release(int room)
        {
            if (!sealedDoors.TryGetValue(room, out var doors)) return;
            sealedDoors.Remove(room);
            clearedAt.Remove(room);
            foreach (var door in doors)
            {
                if (door != null) door.Unlock();
            }
            PlaySound(table != null ? table.ReleasedSound : null);
            Debug.Log($"[Dungeon] モンスターハウス（部屋 {room}）の敵を全部倒した。ドアを開けられる", this);
            Released?.Invoke(room);
        }
    }
}
