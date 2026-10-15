using System;
using UnityEngine;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 階シーンの起点。シーンを再生するとこのコンポーネントがフロアを生成する。
    /// 階ごとにシーンを分け、そのシーンの FloorConfig をここに差すことで層/階のテーマを切り替える。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FloorBuilder))]
    [AddComponentMenu("TPS Dungeon/Floor Bootstrap")]
    public sealed class FloorBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("この階の生成設定。")]
        private FloorConfig config;

        [SerializeField, Tooltip("0 なら毎回ランダム。0 以外を入れるとその形が必ず再現される。")]
        private int seed;

        [SerializeField, Tooltip("シーン再生と同時に生成するか。外したら他のスクリプトから Generate() を呼ぶ。")]
        private bool generateOnStart = true;

        [SerializeField, Tooltip("入口の下り階段の行き先（下の階のシーン名）。1 階なら地上。空なら階段は閉じたまま。")]
        private string downScene = SceneTravel.SurfaceScene;

        [SerializeField, Tooltip("出口の上り階段の行き先（上の階のシーン名）。空ならこの階のシーンを読み直し、シードを引き直した新しい形で作って入口から始める（上の階ができるまでの仮）。")]
        private string upScene;

        // 出口の上り階段でこの階を作り直すとき、次に作る階は seed を固定していても新しい形にする。シーンを読み直してもまたぐよう static に持つ。
        private static bool newShapeOnNextGenerate;

        private FloorBuilder builder;

        /// <summary>実際に使われたシード。seed が 0 のときはランダムに決まった値が入る。</summary>
        public int CurrentSeed { get; private set; }

        public FloorLayout CurrentLayout => builder != null ? builder.CurrentLayout : null;
        public FloorConfig Config => config;

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
            if (config == null)
            {
                Debug.LogError($"{name}: FloorConfig が設定されていないので生成できない", this);
                return;
            }

            if (builder == null) builder = GetComponent<FloorBuilder>();

            CurrentSeed = useSeed;
            var layout = builder.Build(config, useSeed);
            ConnectStair(builder.EntranceStair, downScene);

            // ドメインリロードで null の string は空文字になるので、どちらも「無し」とみなす。
            bool rebuildHere = string.IsNullOrEmpty(upScene);
            var exit = ConnectStair(builder.ExitStair, rebuildHere ? gameObject.scene.name : upScene);
            if (exit != null && rebuildHere) exit.Entering += () => newShapeOnNextGenerate = true;

            Generated?.Invoke(layout);
        }

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
