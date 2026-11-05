using System;
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
    /// ボス階のボス戦。ボス部屋（<see cref="BossFloorLayout.ArenaId"/>）のドア（大扉）が初めて開いたとき、
    /// 階の設定（FloorConfig.boss）のボスを部屋の奥寄りに、開いたドアの方を向けて出す。
    /// ボスが倒れたら閉ざされた扉（出口の扉）を開けられるようにする（<see cref="FloorBuilder.UnlockDoors"/>。出口の階段は FloorBootstrap が繋ぎ直す）。
    /// 階を読み込むたびに出すので、ボス階に来るたびにボスがいる。
    /// 上の階から降りてきた（出口に着いた）ときは、もう倒したものとしてボスを出さずに扉を開けておく（出口の部屋に閉じ込めないように）。
    /// HUD は <see cref="Current"/> を見て、ボスの名前と HP を出す。
    /// ボスが出たら階の設定の曲（FloorConfig.bossBgm）に切り替え、倒したら元の曲に戻す（元が無ければフェードして止める）。
    /// 倒さずに階を離れたとき（全滅・別の階へ移る）も、まだボスの曲が流れていれば同じように戻す。
    /// FloorBuilder と同じ GameObject に付ける（GameFlow が RoomEnemySpawner と一緒に付ける）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FloorBuilder))]
    [AddComponentMenu("TPS Dungeon/Boss Encounter")]
    public sealed class BossEncounter : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.45f), Tooltip("ボスを出す所を、部屋の中心から開いたドアと反対の向きへ、部屋の幅のこの割合だけずらす。")]
        private float backOffset = 0.2f;

        [SerializeField, Min(0f), Tooltip("ボスの曲に切り替えるフェードの秒数。")]
        private float bgmFadeInSeconds = 1f;

        [SerializeField, Min(0f), Tooltip("倒したあと、ボスの曲を止める（元の曲へ戻す）フェードの秒数。")]
        private float bgmFadeOutSeconds = 3f;

        private FloorBuilder builder;
        private FloorBootstrap bootstrap;
        private int arenaRoom = -1;
        private bool spawned;
        private bool defeated = true;
        private AudioClip playingBgm;
        private AudioClip previousBgm;

        /// <summary>今戦っているボス戦（ボスが出ていて、まだ倒れていない）。無ければ null。</summary>
        public static BossEncounter Current { get; private set; }

        /// <summary>出したボスの体力。</summary>
        public EnemyHealth Boss { get; private set; }

        /// <summary>
        /// この階にまだ倒していないボスが居る（出る前も含む）。居る間は、部屋を全部攻略しても攻略の知らせを出さない（RoomEnemySpawner）。
        /// ボスの居ない階、上から降りてきた階、倒したあとは偽。
        /// </summary>
        public bool BossPending => arenaRoom >= 0 && !defeated;

        /// <summary>出したボスの段階。</summary>
        public BossPhases Phases { get; private set; }

        /// <summary>ボスが出た。</summary>
        public static event Action<BossEncounter> AnyStarted;

        /// <summary>ボスが倒れた。</summary>
        public static event Action<BossEncounter> AnyDefeated;

        private void Awake()
        {
            builder = GetComponent<FloorBuilder>();
            bootstrap = GetComponent<FloorBootstrap>();
        }

        private void OnEnable()
        {
            builder.Built += OnBuilt;
            builder.DoorOpened += OnDoorOpened;
            if (builder.CurrentLayout != null) OnBuilt(builder.CurrentLayout);
        }

        private void OnDisable()
        {
            builder.Built -= OnBuilt;
            builder.DoorOpened -= OnDoorOpened;
            Release();
        }

        private FloorConfig Config => bootstrap != null ? bootstrap.Config : null;

        private void OnBuilt(FloorLayout layout)
        {
            Release();
            spawned = false;
            defeated = true;
            arenaRoom = -1;

            FloorConfig config = Config;
            if (config == null || config.layoutShape != FloorLayoutShape.Boss) return;

            foreach (RoomInstance room in layout.Rooms)
            {
                if (room.Template != null && room.Template.Id == BossFloorLayout.ArenaId) arenaRoom = room.Index;
            }

            if (arenaRoom < 0 || config.boss == null || builder.SpawnAtExit)
            {
                // ボスの居ない階や、上から降りてきたときは扉を開けておく。
                spawned = true;
                builder.UnlockDoors();
                return;
            }
            defeated = false;
        }

        private void OnDoorOpened(Door door, RoomEdge edge)
        {
            if (spawned || arenaRoom < 0 || (edge.RoomA != arenaRoom && edge.RoomB != arenaRoom)) return;
            spawned = true;
            Spawn(door);
        }

        private void Spawn(Door door)
        {
            FloorConfig config = Config;
            RoomInstance room = builder.CurrentLayout.RoomAt(arenaRoom);
            float cell = builder.CellSize;
            Vector3 center = builder.transform.TransformPoint(new Vector3(
                (room.Bounds.X + room.Bounds.Width * 0.5f) * cell, 0f, (room.Bounds.Y + room.Bounds.Height * 0.5f) * cell));

            Vector3 away = center - door.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = Vector3.forward;
            away.Normalize();

            float size = Mathf.Min(room.Bounds.Width, room.Bounds.Height) * cell;
            Vector3 at = center + away * (size * backOffset);
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas)) at = hit.position;
            else at = Party.OnNavMesh(center);

            Transform parent = builder.GeneratedRoot != null ? builder.GeneratedRoot : transform;
            GameObject boss = Instantiate(config.boss, at, Quaternion.LookRotation(-away, Vector3.up), parent);
            boss.name = config.boss.name;

            Boss = boss.GetComponent<EnemyHealth>();
            Phases = boss.GetComponent<BossPhases>();
            if (Boss == null)
            {
                Debug.LogError($"ボス {config.boss.name} に EnemyHealth が無いので倒せない。扉を開けておく", config.boss);
                defeated = true;
                builder.UnlockDoors();
                return;
            }

            Boss.Died += OnBossDied;
            Current = this;
            StartBgm(config.bossBgm);
            Debug.Log($"[Dungeon] ボス {boss.name} が出た", this);
            AnyStarted?.Invoke(this);
        }

        private void OnBossDied(EnemyHealth _)
        {
            defeated = true;
            builder.UnlockDoors();
            Debug.Log("[Dungeon] ボスを倒した。扉が開く", this);
            AnyDefeated?.Invoke(this);
            Release();
        }

        private void StartBgm(AudioClip clip)
        {
            GameAudio audio = GameAudio.Instance;
            if (audio == null || clip == null) return;
            previousBgm = audio.CurrentBgm != clip ? audio.CurrentBgm : null;
            playingBgm = clip;
            audio.PlayBgm(clip, bgmFadeInSeconds);
        }

        /// <summary>ボスの曲がまだ流れていれば、元の曲に戻す（無ければ止める）。ほかの曲に替わっていれば触らない（全滅の曲など）。</summary>
        private void EndBgm()
        {
            AudioClip clip = playingBgm;
            AudioClip previous = previousBgm;
            playingBgm = null;
            previousBgm = null;
            GameAudio audio = GameAudio.Instance;
            if (audio == null || clip == null || audio.CurrentBgm != clip) return;
            if (previous != null) audio.PlayBgm(previous, bgmFadeOutSeconds);
            else audio.StopBgm(bgmFadeOutSeconds);
        }

        private void Release()
        {
            EndBgm();
            if (Boss != null) Boss.Died -= OnBossDied;
            if (Current == this) Current = null;
            Boss = null;
            Phases = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            AnyStarted = null;
            AnyDefeated = null;
        }
    }
}
