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

        /// <summary>設定どおりに生成する。seed が 0 なら毎回違う形になる。</summary>
        public void Generate()
        {
            Generate(seed != 0 ? seed : NewRandomSeed());
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
            Generated?.Invoke(layout);
        }

        /// <summary>階段の門に行き先を入れる。行き先が無ければ閉じておく（調べると「閉じている」の案内が出る）。</summary>
        private static void ConnectStair(GameObject stair, string sceneName)
        {
            if (stair == null) return;
            var gate = stair.GetComponentInChildren<SceneGate>();
            if (gate == null) return;

            // ドメインリロードで null の string は空文字になるので、どちらも「無し」とみなす。
            gate.SceneName = sceneName;
            gate.IsOpen = !string.IsNullOrEmpty(sceneName);
        }

        /// <summary>0 は「ランダム」を表す予約値なので避ける。</summary>
        private static int NewRandomSeed()
        {
            int value = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value != 0 ? value : 1;
        }
    }
}
