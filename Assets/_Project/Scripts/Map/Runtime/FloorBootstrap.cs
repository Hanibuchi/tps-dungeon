using System;
using UnityEngine;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 階シーンの起点。シーンを再生するとこのコンポーネントがフロアを生成する。
    /// 道順（DungeonRoute）を差してあれば、着いた階（DungeonProgress）をその道順から読み、その階の FloorConfig で作る。
    /// 下り階段は閉じ（下へは戻れない。案内も出さない）、上り階段は道順の次の階へ繋ぐ（最後の階なら閉じる）。
    /// 道順の AllowGoingDown が入っていれば、下り階段で 1 つ下の階（1 層 1 階なら地上）へ降りられる。
    /// 道順が無ければ（確認用のシーンなど）、このシーンの FloorConfig で作り、階段は downScene / upScene へ繋ぐ。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FloorBuilder))]
    [AddComponentMenu("TPS Dungeon/Floor Bootstrap")]
    public sealed class FloorBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("この階の生成設定。道順を差してあれば道順の方を使う（これは道順に載っていないときの予備）。")]
        private FloorConfig config;

        [SerializeField, Tooltip("ダンジョンの道順。差してあれば、着いた階をこれから読み、上り階段で次の階へ移る。")]
        private DungeonRoute route;

        [SerializeField, Tooltip("道順が無い（載っていない）ときに、部屋に出す敵の表。道順の階なら道順の層の表を使う。空なら敵を出さない。")]
        private EnemySpawnTable enemySpawns;

        [SerializeField, Tooltip("0 なら毎回ランダム。0 以外を入れるとその形が必ず再現される。")]
        private int seed;

        [SerializeField, Tooltip("シーン再生と同時に生成するか。外したら他のスクリプトから Generate() を呼ぶ。")]
        private bool generateOnStart = true;

        [SerializeField, Tooltip("道順が無いときの、入口の下り階段の行き先（下の階のシーン名）。空なら階段は閉じたまま。")]
        private string downScene = SceneTravel.SurfaceScene;

        [SerializeField, Tooltip("道順が無いときの、出口の上り階段の行き先（上の階のシーン名）。空ならこの階のシーンを読み直し、シードを引き直した新しい形で作って入口から始める。")]
        private string upScene;

        // 出口の上り階段でこの階を作り直すとき、次に作る階は seed を固定していても新しい形にする。シーンを読み直してもまたぐよう static に持つ。
        private static bool newShapeOnNextGenerate;

        private FloorBuilder builder;
        private bool floorResolved;
        private FloorConfig activeConfig;

        /// <summary>実際に使われたシード。seed が 0 のときはランダムに決まった値が入る。</summary>
        public int CurrentSeed { get; private set; }

        public FloorLayout CurrentLayout => builder != null ? builder.CurrentLayout : null;

        /// <summary>この階で使っている生成設定（道順の階のもの。道順が無ければこのシーンのもの）。</summary>
        public FloorConfig Config => activeConfig != null ? activeConfig : config;

        public DungeonRoute Route => route;

        /// <summary>この階の部屋に出す敵の表（道順の階ならその層の表、ボス階なら無し。道順が無ければこのシーンのもの）。</summary>
        public EnemySpawnTable EnemySpawns => route != null && CurrentFloor.HasValue ? route.EnemySpawnsOf(CurrentFloor.Value) : enemySpawns;

        /// <summary>道順の中で今いる階。道順が無い・載っていないシーンなら null。</summary>
        public DungeonFloor? CurrentFloor { get; private set; }

        /// <summary>今いる階の表示名（例:「第 1 層 2 階」）。道順が無ければシーン名。</summary>
        public string FloorLabel => route != null && CurrentFloor.HasValue ? route.Label(CurrentFloor.Value) : gameObject.scene.name;

        public event Action<FloorLayout> Generated;

        private void Awake()
        {
            builder = GetComponent<FloorBuilder>();
        }

        private void Start()
        {
            if (generateOnStart) Generate();
        }

        /// <summary>設定どおりに生成する。seed が 0 なら毎回違う形になる（出口の上り階段で作り直すときも違う形）。</summary>
        public void Generate()
        {
            bool newShape = newShapeOnNextGenerate;
            newShapeOnNextGenerate = false;
            Generate(seed != 0 && !newShape ? seed : NewRandomSeed());
        }

        /// <summary>新しいシードを引き直して作り直す。デバッグの再生成用。</summary>
        public void Regenerate()
        {
            Generate(NewRandomSeed());
        }

        public void Generate(int useSeed)
        {
            ResolveFloor();
            var useConfig = Config;
            if (useConfig == null)
            {
                Debug.LogError($"{name}: FloorConfig が設定されていないので生成できない", this);
                return;
            }

            if (builder == null) builder = GetComponent<FloorBuilder>();

            CurrentSeed = useSeed;
            var layout = builder.Build(useConfig, useSeed);

            builder.DoorsUnlocked -= ConnectStairs;
            builder.DoorsUnlocked += ConnectStairs;
            ConnectStairs();
            ConnectShortcut();

            Generated?.Invoke(layout);
        }

        private void OnEnable() => ShortcutUnlocks.Changed += OnShortcutUnlocksChanged;

        private void OnDisable() => ShortcutUnlocks.Changed -= OnShortcutUnlocksChanged;

        private void OnDestroy()
        {
            if (builder != null) builder.DoorsUnlocked -= ConnectStairs;
        }

        /// <summary>今の層（道順が無ければ 0 層とみなす）。ショートカットの解放は層ごと。</summary>
        private int ShortcutLayer => CurrentFloor.HasValue ? CurrentFloor.Value.Layer : 0;

        private void OnShortcutUnlocksChanged(int layer)
        {
            if (layer == ShortcutLayer) ApplyShortcutUnlock();
        }

        /// <summary>ボス階のショートカットの井戸に層を入れ、解放してあれば扉を開けられるようにする。</summary>
        private void ConnectShortcut()
        {
            if (builder == null || builder.ShortcutWell == null) return;
            builder.ShortcutWell.Layer = ShortcutLayer;
            ApplyShortcutUnlock();
        }

        /// <summary>
        /// ショートカットの部屋の扉を、その層の解放状態に合わせる。解放してあれば開けられるようにし、解放を外したら閉ざして閉める。
        /// 井戸の方は自分で解放状態を見る（閉じている間は案内も出さないので、扉の隙間から調べても降りられない）。
        /// </summary>
        private void ApplyShortcutUnlock()
        {
            var door = builder != null ? builder.ShortcutDoor : null;
            if (door == null) return;
            bool unlocked = ShortcutUnlocks.IsUnlocked(ShortcutLayer);
            if (unlocked && door.IsLocked) door.Unlock();
            else if (!unlocked && !door.IsLocked) door.Seal();
        }

        /// <summary>
        /// 階段に行き先を入れる。閉ざされた扉（ボス部屋の奥の出口の扉）がある間は、出口の階段を閉じて案内も出さない。
        /// 扉の隙間から照準が奥の階段に届いても、ボスを倒す（UnlockDoors）までは上れないように。扉を開けられるようになったら繋ぎ直す。
        /// </summary>
        private void ConnectStairs()
        {
            if (builder == null) return;
            if (route != null && CurrentFloor.HasValue) ConnectRouteStairs(CurrentFloor.Value);
            else ConnectSceneStairs();

            if (!builder.HasLockedDoors || builder.ExitStair == null) return;
            var exit = builder.ExitStair.GetComponentInChildren<SceneGate>();
            if (exit == null) return;
            exit.IsOpen = false;
            exit.ClosedLabel = "";
        }

        /// <summary>
        /// 着いた階を決める（最初の生成のときだけ。作り直しでは同じ階のまま）。
        /// 階段で来たならその階、そうでなければ（地上の門から来た・このシーンから再生した）このシーンの最初の階。
        /// </summary>
        private void ResolveFloor()
        {
            if (floorResolved) return;
            floorResolved = true;
            if (route == null) return;

            var pending = DungeonProgress.TakePending();
            bool fromAbove = DungeonProgress.TakePendingFromAbove();
            string sceneName = gameObject.scene.name;
            bool usePending = pending.HasValue && route.SceneOf(pending.Value) == sceneName;
            var floor = usePending ? pending : route.FirstFloorOfScene(sceneName);
            // 上の階から降りてきたら、出口（上り階段）の脇に立つ。
            if (builder == null) builder = GetComponent<FloorBuilder>();
            builder.SpawnAtExit = usePending && fromAbove;
            if (!floor.HasValue)
            {
                Debug.LogWarning($"{sceneName} は道順（{route.name}）に載っていないので、このシーンの設定で作る。", this);
                return;
            }

            SetFloor(floor.Value);
        }

        /// <summary>着いた階を直に決める（次の Generate から使う）。シーン名から決められないテストなど用。</summary>
        public void ArriveAt(DungeonFloor floor)
        {
            if (route == null || !route.Contains(floor)) return;
            floorResolved = true;
            SetFloor(floor);
        }

        private void SetFloor(DungeonFloor floor)
        {
            CurrentFloor = floor;
            DungeonProgress.Current = floor;
            activeConfig = route.ConfigOf(floor);
            Debug.Log($"[Dungeon] {route.Label(floor)}（{floor}、{gameObject.scene.name}）", this);
        }

        /// <summary>今の階の道順で階段を繋ぎ直す（道順の AllowGoingDown を切り替えたときなど）。</summary>
        public void ReconnectStairs() => ConnectStairs();

        /// <summary>
        /// 道順の階段：上りは次の階へ（最後の階なら閉じる）。下りは閉じて案内も出さないが、
        /// AllowGoingDown なら 1 つ下の階（無ければ地上）へ「降りる」。
        /// </summary>
        private void ConnectRouteStairs(DungeonFloor floor)
        {
            var previous = route.Previous(floor);
            string downTo = !route.AllowGoingDown ? null : previous.HasValue ? route.SceneOf(previous.Value) : SceneTravel.SurfaceScene;
            var entrance = ConnectStair(builder.EntranceStair, downTo);
            if (entrance != null)
            {
                entrance.OpenLabel = "降りる";
                entrance.ClosedLabel = "";
                entrance.Entering -= OnEnteringDown;
                if (downTo != null) entrance.Entering += OnEnteringDown;
            }

            var next = route.Next(floor);
            var exit = ConnectStair(builder.ExitStair, next.HasValue ? route.SceneOf(next.Value) : null);
            if (exit == null) return;
            exit.Entering -= OnEnteringUp;
            if (next.HasValue) exit.Entering += OnEnteringUp;
            else exit.ClosedLabel = "この先はまだ無い";
        }

        private void OnEnteringUp() => DungeonProgress.Pending = CurrentFloor.HasValue ? route.Next(CurrentFloor.Value) : null;

        private void OnEnteringDown()
        {
            var previous = CurrentFloor.HasValue ? route.Previous(CurrentFloor.Value) : null;
            DungeonProgress.Pending = previous;
            DungeonProgress.PendingFromAbove = previous.HasValue;
            // 下が地上なら、城の門の前に立つ。
            if (!previous.HasValue) SceneTravel.ArrivalPoint = PlayerSpawnPoint.CastleGate;
        }

        /// <summary>道順が無いときの階段：このシーンに書いた行き先へ。</summary>
        private void ConnectSceneStairs()
        {
            ConnectStair(builder.EntranceStair, downScene);

            // ドメインリロードで null の string は空文字になるので、どちらも「無し」とみなす。
            bool rebuildHere = string.IsNullOrEmpty(upScene);
            var exit = ConnectStair(builder.ExitStair, rebuildHere ? gameObject.scene.name : upScene);
            if (exit == null) return;
            exit.Entering -= OnEnteringRebuildHere;
            if (rebuildHere) exit.Entering += OnEnteringRebuildHere;
        }

        private static void OnEnteringRebuildHere() => newShapeOnNextGenerate = true;

        /// <summary>道順の別の階へ移る（デバッグ用）。行き先の階を Pending に入れてそのシーンへ。今より下の階なら出口の脇に立つ。</summary>
        public void GoToFloor(DungeonFloor floor)
        {
            if (route == null || !route.Contains(floor)) return;
            DungeonProgress.Pending = floor;
            DungeonProgress.PendingFromAbove = CurrentFloor.HasValue && DungeonProgress.IsBelow(floor, CurrentFloor.Value);
            SceneTravel.Go(route.SceneOf(floor));
        }

        /// <summary>地上へ降りる（デバッグ用・1 層 1 階の下り階段）。城の門の前に立つ。</summary>
        public static void GoDownToSurface() => SceneTravel.Go(SceneTravel.SurfaceScene, PlayerSpawnPoint.CastleGate);

        /// <summary>階段の門に行き先を入れる。行き先が無ければ閉じておく（調べると「閉じている」の案内が出る）。</summary>
        private static SceneGate ConnectStair(GameObject stair, string sceneName)
        {
            if (stair == null) return null;
            var gate = stair.GetComponentInChildren<SceneGate>();
            if (gate == null) return null;

            gate.SceneName = sceneName;
            gate.IsOpen = !string.IsNullOrEmpty(sceneName);
            return gate;
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の頼みを持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            newShapeOnNextGenerate = false;
        }

        /// <summary>0 は「ランダム」を表す予約値なので避ける。</summary>
        private static int NewRandomSeed()
        {
            int value = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value != 0 ? value : 1;
        }
    }
}
