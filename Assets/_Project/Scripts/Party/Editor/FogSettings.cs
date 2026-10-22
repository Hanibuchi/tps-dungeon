using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 地上と階の窓の外の霧。2 つある。
    /// - もや（<see cref="MistBuilder"/>）… 塔のまわりにだけかかる霧。地上は塔の上の方を隠す雲（地上の近くには出さない）、階は窓の外の床より下に流す（どちらも始まる高さより上だけ）。
    /// - 遠くの霧（RenderSettings の霧）… 景色全体を遠いほど霞ませる。今は切ってある。霧はシーンごとに持つので、ここで値を持って各シーンへ書き込む。
    /// 景色の生成器（<see cref="SurfaceScenery"/>）もこの値を使うので、生成器を回し直しても調節した値は消えない。
    /// 調節は <see cref="FogWindow"/>（Tools/TPS Dungeon/デバッグ/霧の調節）で。
    /// </summary>
    public sealed class FogSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Settings/Map/FogSettings.asset";

        [Header("塔のまわりのもや")]
        [Tooltip("もやの色。")]
        public Color mistColor = new Color(0.88f, 0.92f, 0.96f);
        [Range(0f, 1f), Tooltip("もや 1 枚の濃さ（不透明さ）。")]
        public float mistOpacity = 0.7f;
        [Min(0f), Tooltip("もやの量（枚数の倍率）。0 で無し。")]
        public float mistAmount = 1f;
        [Min(0.1f), Tooltip("もや 1 枚の大きさの倍率。")]
        public float mistSize = 1f;
        [Min(0f), Tooltip("塔の上の方を隠す雲が始まる、地面からの高さ（メートル）。ここから屋上の少し上までを雲で覆う。階の窓の外のもやもこの高さより上だけに出す。")]
        public float mistStartHeight = 45f;
        [Min(1f), Tooltip("地上で、塔の中心から雲を置く半径（メートル）。塔の一番太い所（半分の幅 70 m ほど）より大きくすると塔を包む。")]
        public float mistRadius = 95f;

        [Header("遠くの霧（景色全体）")]
        [Tooltip("景色全体を遠いほど霞ませるか。")]
        public bool distanceFog;
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

        /// <summary>アセットを読む。無ければ既定の値で作る。</summary>
        public static FogSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FogSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<FogSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>アクティブなシーンの遠くの霧（RenderSettings）にこの値を入れる。</summary>
        public void Apply()
        {
            RenderSettings.fog = distanceFog;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = color;
            RenderSettings.fogStartDistance = startDistance;
            RenderSettings.fogEndDistance = Mathf.Max(startDistance + 1f, endDistance);
            RenderSettings.fogDensity = density;
        }

        /// <summary>アクティブなシーンの遠くの霧（RenderSettings）の値を読み込む。</summary>
        public void ReadFromScene()
        {
            distanceFog = RenderSettings.fog;
            mode = RenderSettings.fogMode;
            color = RenderSettings.fogColor;
            startDistance = RenderSettings.fogStartDistance;
            endDistance = RenderSettings.fogEndDistance;
            density = RenderSettings.fogDensity;
        }
    }
}
