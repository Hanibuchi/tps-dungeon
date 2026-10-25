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
    /// モンスターハウス（普通の部屋が表の確率でなる）のドアを外から開けると、その部屋と、開けた側の部屋（先頭が居る部屋）の 2 部屋に閉じ込める。
    /// 誰も跳ばさない（跳ばすと自分の居場所を見失う）。開けたドアは開いたまま 2 部屋をつなぎ、ほかのドアを全部閉ざして閉める
    /// （ボス部屋の奥の扉と同じく「閉ざされている」）。2 部屋の外に残っていた仲間だけ、先頭の居る部屋へ移す。
    /// 2 部屋の敵を全部倒して releaseDelay 秒（既定 1 秒）経つと、閉ざしたドアを開けられるようにする。
    /// 閉じ込めたとき・鍵が開いたときに、層の表の音を鳴らす。モンスターハウスの予算の上限は表の倍率を掛けたもの。
    /// 敵の出る部屋（RoomEnemyRoller.CanHostEnemies）を全部開け、その敵（箱から出た敵も）が全部居なくなって、閉じ込めも解けてから
    /// clearDelay 秒経つと、階の部屋を全部攻略したと 1 度だけ知らせる（<see cref="AllRoomsCleared"/>）。
    /// 残りは倒れた知らせ（EnemyHealth.Died）では数えず、閉じ込めている間に毎フレーム数え直す。自爆する敵は Died を出さずに消え、
    /// ドアが閉まりきる前に 2 部屋の外へ出た敵は中から倒せないので、消えた・倒れた・2 部屋の外に居る敵は残りに入れない。
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

        [SerializeField, Min(0f), Tooltip("モンスターハウスの最後の敵が居なくなってから、ドアの鍵を開ける（音を鳴らす）までの間（秒）。倒した瞬間の音と重ねないように。")]
        private float releaseDelay = 1f;

        [SerializeField, Min(0f), Tooltip("階の最後の敵が居なくなってから（閉じ込めていたら鍵が開いてから）、全部屋攻略を知らせるまでの間（秒）。「扉が開いた」の見出しと重ねないように。")]
        private float clearDelay = 2f;

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
        // 閉じ込めているモンスターハウスの部屋ごとの、閉じ込めの記録。
        private readonly Dictionary<int, Trap> traps = new Dictionary<int, Trap>();
        // 敵の出る部屋の数。0 の階（敵の出ない階）は攻略を知らせない。
        private int hostRooms;
        private bool allRoomsCleared;
        // 攻略の条件がそろった時刻。そろっていなければ null。
        private float? allClearedAt;

        /// <summary>1 つの閉じ込め。モンスターハウスと開けた側の部屋、閉ざしたドア。</summary>
        private sealed class Trap
        {
            public int House;
            public int[] Rooms;
            public List<Door> Doors;
            // 敵が居なくなった時刻。居る間は null。
            public float? ClearedAt;
        }

        /// <summary>モンスターハウスに閉じ込めた（部屋の番号）。</summary>
        public event Action<int> Trapped;

        /// <summary>モンスターハウスの敵を全部倒して、閉ざしたドアを開けられるようにした（部屋の番号）。</summary>
        public event Action<int> Released;

        /// <summary>階の部屋を全部攻略した（階ごとに 1 度）。</summary>
        public event Action AllRoomsCleared;

        /// <summary>どこかの階の部屋を全部攻略した（シーンをまたいで残る側が聞く）。</summary>
        public static event Action<RoomEnemySpawner> AnyAllRoomsCleared;

        /// <summary>この階の部屋を全部攻略したか。</summary>
        public bool IsAllRoomsCleared => allRoomsCleared;

        /// <summary>この階のモンスターハウスの部屋の番号。</summary>
        public IReadOnlyCollection<int> MonsterHouses => monsterHouses;

        /// <summary>閉じ込められているか。</summary>
        public bool IsTrapped => traps.Count > 0;

        /// <summary>閉じ込めている範囲に残っている敵の数。閉じ込めていなければ 0。</summary>
        public int AliveInTrap
        {
            get
            {
                int count = 0;
                foreach (var trap in traps.Values) count += AliveIn(trap);
                return count;
            }
        }

        /// <summary>閉じ込めた部屋に出した敵のうち、消えておらず、倒れておらず、まだ閉じ込めた部屋のどれかに居る数。</summary>
        private int AliveIn(Trap trap)
        {
            if (layout == null) return 0;
            int count = 0;
            foreach (int room in trap.Rooms)
            {
                if (!spawnedEnemies.TryGetValue(room, out var enemies)) continue;
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.IsDead && InAnyRoom(trap.Rooms, enemy.transform.position)) count++;
                }
            }
            return count;
        }

        private bool InAnyRoom(int[] rooms, Vector3 world)
        {
            var cell = builder.WorldToCell(world);
            foreach (int room in rooms)
            {
                if (layout.RoomAt(room).Bounds.Contains(cell)) return true;
            }
            return false;
        }

        private void Update()
        {
            UpdateTraps();
            UpdateAllCleared();
        }

        /// <summary>閉じ込めている範囲の敵が居なくなったら、releaseDelay 秒待ってから鍵を開ける。</summary>
        private void UpdateTraps()
        {
            if (traps.Count == 0) return;
            List<int> ready = null;
            foreach (var trap in traps.Values)
            {
                if (AliveIn(trap) > 0)
                {
                    trap.ClearedAt = null;
                    continue;
                }
                trap.ClearedAt ??= Time.time;
                if (Time.time - trap.ClearedAt.Value >= releaseDelay) (ready ??= new List<int>()).Add(trap.House);
            }
            if (ready == null) return;
            foreach (int house in ready) Release(house);
        }

        /// <summary>敵の出る部屋を全部開け、敵が全部居なくなり、閉じ込めも解けたら、clearDelay 秒待ってから攻略を知らせる。</summary>
        private void UpdateAllCleared()
        {
            if (allRoomsCleared || hostRooms == 0 || layout == null) return;
            if (traps.Count > 0 || !AllHostRoomsOpened() || AnyEnemyLeft())
            {
                allClearedAt = null;
                return;
            }
            allClearedAt ??= Time.time;
            if (Time.time - allClearedAt.Value < clearDelay) return;

            allRoomsCleared = true;
            Debug.Log("[Dungeon] 階の部屋を全部攻略した", this);
            AllRoomsCleared?.Invoke();
            AnyAllRoomsCleared?.Invoke(this);
        }

        private bool AllHostRoomsOpened()
        {
            foreach (var room in layout.Rooms)
            {
                if (RoomEnemyRoller.CanHostEnemies(room) && !spawnedRooms.Contains(room.Index)) return false;
            }
            return true;
        }

        /// <summary>出した敵（箱から出た敵も）のうち、消えておらず倒れていないものが居るか。部屋の外へ出ていても数える。</summary>
        private bool AnyEnemyLeft()
        {
            foreach (var enemies in spawnedEnemies.Values)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.IsDead) return true;
                }
            }
            return false;
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
            traps.Clear();
            enemyRoot = null;
            allRoomsCleared = false;
            allClearedAt = null;
            hostRooms = 0;
            foreach (var room in layout.Rooms)
            {
                if (RoomEnemyRoller.CanHostEnemies(room)) hostRooms++;
            }
            // ボス階ではモンスターハウスにしない（閉じ込めると大扉やショートカットの部屋の扉まで閉ざしてしまう）。
            bool bossFloor = bootstrap != null && bootstrap.Config != null && bootstrap.Config.layoutShape == FloorLayoutShape.Boss;
            monsterHouses = bossFloor ? new HashSet<int>() : RoomEnemyRoller.PickMonsterHouses(rules, layout);
            if (monsterHouses.Count > 0) Debug.Log($"[Dungeon] モンスターハウス: 部屋 {string.Join(", ", monsterHouses)}", this);
            PrepareBreakables();
        }

        /// <summary>
        /// 部屋の木箱・宝箱に、この層の敵（表の敵。重みはコストの逆数）を差し、出てきた敵を生成物の下に置く。
        /// 出てきた敵はその部屋に出した敵にも数える（モンスターハウスで閉じ込めている間に箱から出たら、それも倒すまで出られない）。
        /// </summary>
        private void PrepareBreakables()
        {
            if (builder.GeneratedRoot == null) return;

            var prefabs = new List<GameObject>();
            var weights = new List<float>();
            if (table != null)
            {
                foreach (var enemy in table.Enemies)
                {
                    prefabs.Add(enemy.prefab);
                    // 強い（コストの高い）敵ほど出にくい。
                    weights.Add(enemy.cost > 0f ? 1f / enemy.cost : 0f);
                }
            }

            foreach (var prop in builder.GeneratedRoot.GetComponentsInChildren<BreakableProp>(true))
            {
                if (prop.DropTable == null) continue;
                prop.SetEnemyPool(prefabs, weights);
                prop.SetWeaponRanks(bootstrap != null && bootstrap.Config != null ? bootstrap.Config.boxWeaponRanks : null);
                prop.SpawnParent = EnemyRoot();
                int room = builder.CurrentOccupancy != null ? builder.CurrentOccupancy.RoomAt(builder.WorldToCell(prop.transform.position)) : -1;
                prop.Spawned += (_, spawned) => OnBreakableSpawned(room, spawned);
            }
        }

        private void OnBreakableSpawned(int room, GameObject spawned)
        {
            if (room < 0 || spawned == null || !spawned.TryGetComponent(out EnemyHealth health)) return;
            if (!spawnedEnemies.TryGetValue(room, out var enemies)) spawnedEnemies[room] = enemies = new List<EnemyHealth>();
            enemies.Add(health);
        }

        private void OnDoorOpened(Door door, RoomEdge edge)
        {
            if (rules == null || layout == null) return;
            foreach (int room in new[] { edge.RoomA, edge.RoomB })
            {
                if (!spawnedRooms.Add(room)) continue;

                var groups = RoomEnemyRoller.RollRoom(rules, layout, room, monsterHouses.Contains(room));
                if (groups.Count == 0) continue;

                // 外から開けたモンスターハウスなら、外に残った仲間を手前の部屋へ入れてから（パーティーの近くを避けて）敵を出し、
                // 2 部屋のドアを開けたドアのほかは閉ざす。
                bool trap = monsterHouses.Contains(room) && !LeaderInside(room);
                int[] trapRooms = trap ? new[] { room, room == edge.RoomA ? edge.RoomB : edge.RoomA } : null;
                if (trap) GatherParty(trapRooms, edge);
                Spawn(room, groups, trap);
                if (trap) SealRooms(room, trapRooms, door);
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
        /// 閉じ込める部屋（rooms）の外に居る仲間を、手前の部屋（rooms[1]、先頭が居る側）の先頭の近くへ移す。先頭は動かさない。
        /// 跡も、先頭から手前の部屋の奥へ向けて作り直す。前の跡は閉ざすドアの外を通っていて、仲間がそれを追うとドアの前に溜まってしまう。
        /// </summary>
        private void GatherParty(int[] rooms, RoomEdge edge)
        {
            var party = Party.Current;
            if (party == null || party.Leader == null) return;

            int near = rooms[1];
            Transform leader = party.Leader.transform;
            var socket = edge.RoomA == near ? edge.SocketA : edge.SocketB;
            var step = socket.Facing.Offset();
            Vector3 inward = builder.transform.TransformDirection(new Vector3(-step.X, 0f, -step.Y));
            party.Trail.Reset(leader.position, inward, Mathf.Min(party.Spacing * party.Members.Count, builder.CellSize * 0.5f));

            var outside = new List<PartyMember>();
            for (int i = 1; i < party.Members.Count; i++)
            {
                var member = party.Members[i];
                if (member != null && !InAnyRoom(rooms, member.transform.position)) outside.Add(member);
            }
            if (outside.Count == 0) return;

            var points = FollowerPoints(layout.RoomAt(near), leader.position, outside.Count);
            for (int i = 0; i < outside.Count; i++) outside[i].PlaceAt(points[i], leader.rotation);
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

        /// <summary>
        /// 閉じ込める部屋のドアを、開けたドア（opened）のほかは全部閉ざして閉める。
        /// もともと閉ざされていた扉（ボス部屋の奥など）は開けるときに触らないよう控えない。
        /// </summary>
        private void SealRooms(int house, int[] rooms, Door opened)
        {
            var doors = new List<Door>();
            foreach (int room in rooms)
            {
                foreach (var door in builder.DoorsOf(room))
                {
                    if (door == opened || door.IsLocked || doors.Contains(door)) continue;
                    door.Seal();
                    doors.Add(door);
                }
            }
            traps[house] = new Trap { House = house, Rooms = rooms, Doors = doors };
            PlaySound(table.TrappedSound);
            Debug.Log($"[Dungeon] モンスターハウス（部屋 {house}）に閉じ込めた（部屋 {string.Join(", ", rooms)}）", this);
            Trapped?.Invoke(house);
        }

        /// <summary>閉じ込め・鍵が開く音。パーティーは部屋の中に居るので、位置を付けずに鳴らす。</summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip != null) GameAudio.Instance?.PlaySe(clip, table.SoundVolume);
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の購読を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnyAllRoomsCleared = null;

        private void Release(int room)
        {
            if (!traps.TryGetValue(room, out var trap)) return;
            traps.Remove(room);
            foreach (var door in trap.Doors)
            {
                if (door != null) door.Unlock();
            }
            PlaySound(table != null ? table.ReleasedSound : null);
            Debug.Log($"[Dungeon] モンスターハウス（部屋 {room}）の敵を全部倒した。ドアを開けられる", this);
            Released?.Invoke(room);
        }
    }
}
