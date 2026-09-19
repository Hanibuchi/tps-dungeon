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

        [SerializeField]
        private bool generateOnStart = true;

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
            Generated?.Invoke(layout);
        }

        /// <summary>0 は「ランダム」を表す予約値なので避ける。</summary>
        private static int NewRandomSeed()
        {
            int value = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value != 0 ? value : 1;
        }
    }
}
