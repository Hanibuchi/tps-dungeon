using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上と階の窓の外の霧（RenderSettings の霧）。霧はシーンごとに持つので、ここで値を持って各シーンへ書き込む。
    /// 景色の生成器（<see cref="SurfaceScenery"/>）もこの値を使うので、生成器を回し直しても調節した値は消えない。
    /// 調節は <see cref="FogWindow"/>（Tools/TPS Dungeon/霧の調節）で。
    /// </summary>
    public sealed class FogSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Settings/Map/FogSettings.asset";

        [Tooltip("霧の色。空の色に寄せると遠くが空に溶ける。")]
        public Color color = new Color(0.75f, 0.88f, 0.94f);
        [Tooltip("Linear は「始まる距離」から「真っ白になる距離」までだんだん濃くなる。Exponential・ExponentialSquared は「濃さ」で決まる。")]
        public FogMode mode = FogMode.Linear;
        [Min(0f), Tooltip("Linear のとき、霧がかかり始める距離（メートル）。")]
        public float startDistance = 70f;
        [Min(0f), Tooltip("Linear のとき、霧の色一色になる距離（メートル）。")]
        public float endDistance = 650f;
        [Min(0f), Tooltip("Exponential・ExponentialSquared のときの濃さ。")]
        public float density = 0.01f;

        /// <summary>アセットを読む。無ければ今の値（生成器が使っていた値）で作る。</summary>
        public static FogSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FogSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<FogSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>アクティブなシーンの霧（RenderSettings）にこの値を入れる。</summary>
        public void Apply()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = color;
            RenderSettings.fogStartDistance = startDistance;
            RenderSettings.fogEndDistance = Mathf.Max(startDistance + 1f, endDistance);
            RenderSettings.fogDensity = density;
        }

        /// <summary>アクティブなシーンの霧（RenderSettings）の値を読み込む。</summary>
        public void ReadFromScene()
        {
            mode = RenderSettings.fogMode;
            color = RenderSettings.fogColor;
            startDistance = RenderSettings.fogStartDistance;
            endDistance = RenderSettings.fogEndDistance;
            density = RenderSettings.fogDensity;
        }
    }
}
