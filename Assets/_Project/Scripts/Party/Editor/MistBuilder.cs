using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 塔のまわりのもや。大きな柔らかい板（パーティクル）をゆっくり漂わせて、景色全体ではなく塔のまわりにだけ霧がかかって見せる。
    /// - 地上: 塔の根元を囲む輪と、塔の 4 つの面に沿った高さごとの帯（<see cref="FogSettings.mistHeight"/> まで）。帯は塔の足元の四角（上ほど太い）に合わせる。
    /// - 階: 窓の外の、床より下（地面より上）に一面に流す。部屋の中に入り込まないよう、板の上の端が床より下に来るようにする。
    /// シーンのルートの [Mist] にまとめて置き直す。色と濃さは素材（<see cref="MaterialPath"/>）に持つので、変えると全部のシーンにすぐ効く。
    /// 量・大きさ・高さは置き直さないと変わらない（霧の調節の窓が開いているシーンを置き直す）。
    /// </summary>
    public static class MistBuilder
    {
        public const string RootName = "[Mist]";
        private const string MaterialPath = "Assets/_Project/Materials/Effects/Mat_Mist.mat";
        private const string TexturePath = "Assets/_Project/Materials/Effects/Tex_Mist.png";

        // 塔の足元の真ん中（地上の座標）。根元の輪の真ん中。
        private static readonly Vector3 TowerCenter = new Vector3(0f, 0f, 48f);

        /// <summary>地上のシーンに、塔のまわりのもやを置き直す。</summary>
        public static string BuildSurface(Scene scene, FogSettings settings)
        {
            Transform root = NewRoot(scene);
            Material material = LoadOrCreateMaterial(settings);
            int count = 0;

            // 根元を囲む輪。広場の門の前にも少しかかる。
            count += Ring(root, material, settings, "Mist_Ground", TowerCenter + Vector3.up * 4f, 28f, 75f, 6f, 70, 24f, 40f);

            // 塔の側面。高さごとに、その高さの足元の四角の 4 つの面に沿って帯を置く（面の少し内側から外へ 16 m）。
            float top = SurfaceTower.TopHeight();
            for (float h = 25f; h <= Mathf.Min(settings.mistHeight, top) + 0.01f; h += 25f)
            {
                Rect foot = SurfaceTower.FootAtHeight(h);
                const float inside = 3f, outside = 16f, reach = inside + outside;
                float perimeter = 2f * (foot.width + foot.height);
                var sides = new (string name, Vector3 center, Vector3 scale, float length)[]
                {
                    ("S", new Vector3(foot.center.x, h, foot.yMin - outside + reach * 0.5f), new Vector3(foot.width + outside * 2f, 10f, reach), foot.width),
                    ("N", new Vector3(foot.center.x, h, foot.yMax + outside - reach * 0.5f), new Vector3(foot.width + outside * 2f, 10f, reach), foot.width),
                    ("W", new Vector3(foot.xMin - outside + reach * 0.5f, h, foot.center.y), new Vector3(reach, 10f, foot.height), foot.height),
                    ("E", new Vector3(foot.xMax + outside - reach * 0.5f, h, foot.center.y), new Vector3(reach, 10f, foot.height), foot.height),
                };
                foreach (var side in sides)
                {
                    // 1 段あたり 40 枚を、面の長さで 4 つの面に分ける。
                    var system = NewSystem(root, $"Mist_{h:0}m_{side.name}", material, settings, side.center,
                        Mathf.RoundToInt(40f * side.length / perimeter), 24f * settings.mistSize, 42f * settings.mistSize);
                    if (system == null) continue;
                    var shape = system.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = side.scale;
                    count += system.main.maxParticles;
                }
            }
            return $"塔のまわりのもやを置いた（{root.childCount} か所、多くて {count} 枚）。";
        }

        /// <summary>
        /// 階のシーンに、窓の外の床より下のもやを置き直す。center は階の真ん中の床の高さ（ワールド座標）、elevation は床から地面までの高さ、extent は階の枠の一辺（メートル）。
        /// </summary>
        public static string BuildFloor(Scene scene, Vector3 center, float elevation, float extent, FogSettings settings)
        {
            Transform root = NewRoot(scene);
            Material material = LoadOrCreateMaterial(settings);

            // 板の上の端が床の 4 m 下、下の端が地面の 3 m 上（高い階は床の 50 m 下まで）に収まるようにする。
            float high = center.y - 4f;
            float low = center.y - Mathf.Min(Mathf.Max(elevation - 3f, 6f), 50f);
            float maxSize = Mathf.Min(36f * settings.mistSize, high - low);
            float thickness = Mathf.Max(0f, high - low - maxSize);

            // 低い階は板が小さくなるので、そのぶん枚数を増やして同じくらい埋める。
            float area = extent * extent * 1.15f * 1.15f;
            int count = Mathf.RoundToInt(Mathf.Clamp(area / (maxSize * maxSize) * 0.6f, 160f, 600f));
            var system = NewSystem(root, "Mist_Below", material, settings, new Vector3(center.x, (high + low) * 0.5f, center.z), count, maxSize * 0.6f, maxSize);
            if (system == null) return "窓の外のもやは量が 0 なので置かなかった。";
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(extent * 1.15f, thickness, extent * 1.15f);
            shape.rotation = Vector3.zero;
            return $"窓の外のもやを置いた（床の下 {center.y - high:0}〜{center.y - low:0} m）。";
        }

        /// <summary>素材の色と濃さを設定に合わせる（全部のシーンにすぐ効く）。</summary>
        public static void ApplyColor(FogSettings settings)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) return;
            Color color = settings.mistColor;
            color.a = settings.mistOpacity;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }

        /// <summary>中心 center の水平な輪（内側 inner〜外側 outer）にもやを漂わせる。spread は上下と横へのばらつき。置ける枚数を返す。</summary>
        private static int Ring(Transform root, Material material, FogSettings settings, string name, Vector3 center, float inner, float outer, float spread,
            int count, float minSize, float maxSize)
        {
            var system = NewSystem(root, name, material, settings, center, count, minSize * settings.mistSize, maxSize * settings.mistSize);
            if (system == null) return 0;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = outer;
            shape.radiusThickness = (outer - inner) / outer;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.randomPositionAmount = spread;
            return system.main.maxParticles;
        }

        /// <summary>もやの板を出すパーティクルを 1 つ作る（形は呼ぶ側が決める）。量が 0 なら作らない。</summary>
        private static ParticleSystem NewSystem(Transform root, string name, Material material, FogSettings settings, Vector3 position, int count,
            float minSize, float maxSize)
        {
            int max = Mathf.RoundToInt(count * settings.mistAmount);
            if (max <= 0) return null;

            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            const float minLife = 50f, maxLife = 80f;
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = true;

            var emission = system.emission;
            emission.rateOverTime = max / ((minLife + maxLife) * 0.5f);

            // 生まれてすぐと消える前は薄くして、ふっと現れて消える。
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            // 近くの大きな板が画面の大きさで切られないように。
            renderer.maxParticleSize = 5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static Transform NewRoot(Scene scene)
        {
            foreach (GameObject old in scene.GetRootGameObjects())
            {
                if (old.name == RootName) Object.DestroyImmediate(old);
            }
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        // --- 素材 ---

        /// <summary>
        /// もやの素材。無ければ作る（URP の Particles/Unlit の半透明。地面や壁との境目をぼかす soft particles と、カメラに近い板を薄くする camera fading を入れる）。
        /// 色と濃さは毎回設定に合わせる。
        /// </summary>
        private static Material LoadOrCreateMaterial(FogSettings settings)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Mat_Mist" };
                material.SetTexture("_BaseMap", LoadOrCreateTexture());
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SoftParticlesEnabled", 1f);
                material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
                material.SetFloat("_SoftParticlesFarFadeDistance", 6f);
                material.SetFloat("_CameraFadingEnabled", 1f);
                material.SetFloat("_CameraNearFadeDistance", 3f);
                material.SetFloat("_CameraFarFadeDistance", 18f);
                ValidateUrpMaterial(material);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            ApplyColor(settings);
            return material;
        }

        /// <summary>URP の素材の検証（キーワード・描画順・混ぜ方）を素材の窓と同じように通す。あとで URP が検証し直しても変わらないように。</summary>
        private static void ValidateUrpMaterial(Material material)
        {
            Type baseGui = Type.GetType("UnityEditor.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor");
            Type particleGui = Type.GetType("UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI, Unity.RenderPipelines.Universal.Editor");
            MethodInfo particleKeywords = particleGui?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
            MethodInfo keywords = baseGui?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static);
            if (keywords == null || particleKeywords == null)
            {
                Debug.LogWarning("URP の素材の検証が見つからないので、もやの素材のキーワードを揃えられなかった。素材を一度インスペクタで開くこと。");
                return;
            }
            var shaderFunc = (Action<Material>)Delegate.CreateDelegate(typeof(Action<Material>), particleKeywords);
            keywords.Invoke(null, new object[] { material, null, shaderFunc });
        }

        /// <summary>もやの絵（白い雲。縁へ向かって薄くなる丸に、ゆらぎを掛けた透明度）。無ければ作る。</summary>
        private static Texture2D LoadOrCreateTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null) return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float edge = Mathf.Clamp01(1f - r);
                    edge = edge * edge * (3f - 2f * edge);
                    float noise = 0f, amplitude = 0.5f, frequency = 3f;
                    for (int o = 0; o < 4; o++)
                    {
                        noise += amplitude * Mathf.PerlinNoise(x / (float)size * frequency + 17.3f * o, y / (float)size * frequency + 5.1f * o);
                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }
                    float alpha = Mathf.Clamp01(edge * (0.35f + noise * 0.9f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
