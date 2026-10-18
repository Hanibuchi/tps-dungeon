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
    /// 塔のまわりのもや。大きな柔らかい板（パーティクル）をゆっくり漂わせて、景色全体ではなく塔のまわりにだけ霧がかかって見せる。地上では塔の上の方を雲で隠すのが役目。
    /// - 地上: 塔の上の方を隠す雲。塔の中心から半径 <see cref="FogSettings.mistRadius"/> の円柱の中に、地上 <see cref="FogSettings.mistStartHeight"/> m から屋上の少し上まで。地上の近くには出さない。
    /// - 階: 窓の外の、床より下（地面より上）に一面に流す。部屋の中に入り込まないよう、板の上の端が床より下に来るようにする。
    /// シーンのルートの [Mist] にまとめて置き直す。色と濃さは素材（<see cref="MaterialPath"/>）に持つので、変えると全部のシーンにすぐ効く。
    /// 量・大きさ・半径・始まる高さは置き直さないと変わらない（霧の調節の窓が開いているシーンを置き直す）。
    /// </summary>
    public static class MistBuilder
    {
        public const string RootName = "[Mist]";
        private const string MaterialPath = "Assets/_Project/Materials/Effects/Mat_Mist.mat";
        private const string TexturePath = "Assets/_Project/Materials/Effects/Tex_Mist.png";

        /// <summary>地上のシーンに、塔のまわりのもやを置き直す。</summary>
        public static string BuildSurface(Scene scene, FogSettings settings)
        {
            Transform root = NewRoot(scene);
            Material material = LoadOrCreateMaterial(settings);
            int count = 0;

            // 塔の上の方を隠す雲。始まる高さから屋上の少し上まで、塔の中心から半径 mistRadius の円柱の中に、高さ 12 m ごとに輪を重ねる。
            // 外から見て塔を覆うのは外側の板なので、輪は外寄り（半径の 45 % より外）に置く。枚数は輪の周りの長さに比例させる。
            float top = SurfaceTower.TopHeight() + 25f;
            float radius = settings.mistRadius;
            for (float h = settings.mistStartHeight; h <= top + 0.01f; h += 12f)
            {
                Rect foot = SurfaceTower.FootAtHeight(h);
                var center = new Vector3(foot.center.x, h, foot.center.y);
                int perRing = Mathf.RoundToInt(2f * Mathf.PI * radius / 5f);
                count += Ring(root, material, settings, $"Mist_{h:0}m", center, radius * 0.45f, radius, 7f, perRing, 30f, 50f);
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
            StartIfPlaying(system);
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
            StartIfPlaying(system);
            return system.main.maxParticles;
        }

        /// <summary>
        /// Play 中（霧の調節の窓で置き直したとき）は、出きった状態から再生する。作るときに長さ（duration）を変えるため止めてあり、
        /// 付け足したときの再生（playOnAwake）は済んでいるので、そのままだと止まったまま。途中から再生すると prewarm も効かないので、
        /// 一番長い寿命の分まで進めてから続ける。形（shape）を決めてから呼ぶこと（先に進めると、決める前の形と大きさで出た板が残る）。
        /// </summary>
        private static void StartIfPlaying(ParticleSystem system)
        {
            if (!Application.isPlaying) return;
            system.Simulate(system.main.startLifetime.constantMax, true, true);
            system.Play(true);
        }

        /// <summary>もやの板を出すパーティクルを 1 つ作る（形は呼ぶ側が決めて、そのあと <see cref="StartIfPlaying"/> を呼ぶ）。量が 0 なら作らない。</summary>
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
