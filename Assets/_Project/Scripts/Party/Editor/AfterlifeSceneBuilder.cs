using System.Collections.Generic;
using TpsDungeon.Enemies;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 死後の場所（水鏡の底）のシーンを作る。何度実行しても同じ形に作り直す。
    /// - 全員が倒れると GameFlow がここへ送る。パーティーは魂の姿（<see cref="SoulForm"/>）になり、強化の台（<see cref="UpgradeAltar"/>）と転生の渦（地上への <see cref="SceneGate"/>）がある。
    /// - どこまでも続く黒く浅い水面に、平たい石の足場。着地点 → 燭台の輪（強化）→ 転生の渦 を +Z へ一直線に並べ、端から端まで走って 5 秒ほど。
    /// - 頭上の遠くに、地上の城の塔のメッシュ（Tower_*）を上下逆さにして垂らす。上ほど太い城が、ここでは下ほど太い。
    /// - 寒色・低い彩度で暗く、主な光は魂（SoulForm が一人ずつに付ける光）と燭台の青白い火。光の粒がゆっくり沈んでいく。
    /// - 材質と Volume の profile は無いときだけ作る（手で調節した値を上書きしない）。
    /// - 直接 Play して確かめられるよう、地上と同じパーティー一式（[Party Rig]）も置く。地上から来たときは GameFlow がこちらを消す。
    /// </summary>
    public static class AfterlifeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Afterlife.unity";
        private const string NavMeshPath = "Assets/_Project/Scenes/Afterlife/NavMesh-Afterlife.asset";
        private const string ReflectionPath = "Assets/_Project/Scenes/Afterlife/ReflectionProbe-Afterlife.exr";
        private const string MaterialsFolder = "Assets/_Project/Materials/Afterlife";
        private const string VolumeProfilePath = "Assets/_Project/Materials/Afterlife/Afterlife_VolumeProfile.asset";
        private const string TowerMeshFolder = "Assets/_Project/Scenes/Surface";
        private const string StoneSourcePath = "Assets/ThirdParty/Material/YughuesFreePavementsMaterials/Materials/M_YFPM_JaggedSquare.mat";

        // 歩ける範囲（メートル）。+Z が転生の渦。まわりを見えない壁で囲む。
        private const float AreaMinX = -14f;
        private const float AreaMaxX = 14f;
        private const float AreaMinZ = -8f;
        private const float AreaMaxZ = 32f;
        private const float BoundsHeight = 20f;
        private const float BoundsThickness = 2f;

        // 3 か所の足場の真ん中。
        private static readonly Vector3 Landing = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AltarCenter = new Vector3(-4.5f, 0f, 11f);
        private static readonly Vector3 PortalCenter = new Vector3(0f, 0f, 22f);

        // 足場は水面（y = 0）から少しだけ出る。
        private const float StoneTop = 0.12f;
        private const int Candles = 6;
        private const float CandleRadius = 3.2f;

        // 逆さの城。いちばん低い所（地上の城のてっぺん）をこの高さに、この奥に置く。
        private const float InvertedCastleLowest = 6f;
        private const float InvertedCastleDistance = 110f;
        private const float InvertedCastleScale = 0.7f;

        private static readonly Color FogColor = new Color(0.06f, 0.11f, 0.14f);

        [MenuItem("Tools/TPS Dungeon/死後の場所のシーンを作る")]
        public static void GenerateFromMenu() => Debug.Log(Generate());

        /// <summary>死後の場所のシーンを作り直して保存し、何をしたかを返す。</summary>
        public static string Generate()
        {
            var partyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.PartyPrefabPath);
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartySetup.CharacterPrefabPath);
            var followCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurfaceSceneBuilder.FollowCameraPrefabPath);
            if (partyPrefab == null || characterPrefab == null) return "先に Tools/TPS Dungeon/Party/パーティーを組み込む を実行すること。";
            if (followCameraPrefab == null) return $"追従カメラのプレハブが無い: {SurfaceSceneBuilder.FollowCameraPrefabPath}";

            EnsureFolder(MaterialsFolder);
            var m = new Materials();
            if (m.Soul == null) return "魂のシェーダー（TpsDungeon/Soul）が見つからない。";

            // 開いているシーンへ上書き保存すると黙って失敗するので、Afterlife が開いていたら先に閉じる（作り直すので変更は捨てる）。
            // それしか開いていなければ、空のシーンに替えてそこへ作り、作ったあと開き直す。
            bool alone = ReleaseOpenScene();

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = alone ? previousActive : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            string castle;
            try
            {
                CreateLighting(m);
                CreateVolume();

                Transform world = new GameObject("[Afterlife]").transform;
                Transform walkable = Group(world, "Walkable");
                BuildWater(walkable, m);
                BuildStones(walkable, m);
                BuildAltar(world, walkable, m);
                BuildPortal(world, m);
                castle = BuildInvertedCastle(world, m);
                BuildReflection(world);
                BuildMotes(world, m);
                BuildBounds();
                Transform spawn = CreateSpawn(world);

                var soulForm = new SerializedObject(new GameObject("Soul Form").AddComponent<SoulForm>());
                soulForm.FindProperty("soulMaterial").objectReferenceValue = m.Soul;
                soulForm.ApplyModifiedPropertiesWithoutUndo();
                SurfaceSceneBuilder.PlaceParty(spawn, partyPrefab, characterPrefab, followCameraPrefab);

                // 保存してからでないと NavMesh のアセットの置き場を決められないので、先に 1 度保存する。
                EditorSceneManager.SaveScene(scene, ScenePath);
                BakeNavMesh(walkable.gameObject);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
            }
            finally
            {
                if (!alone)
                {
                    if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (alone) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return $"{ScenePath} を作った（着地点 → 燭台 {Candles} 本の強化の台 → 転生の渦（地上へ）、{castle}）。";
        }

        /// <summary>Afterlife が開いていれば閉じる。それしか開いていなければ空のシーンに替えて true（そこへ作る）。名前の無い空のシーンだけが開いているときも true。</summary>
        private static bool ReleaseOpenScene()
        {
            // 名前の無い空のシーンしか開いていないときは、追加で開けない（Unity が断る）ので、そこへ作る。
            Scene active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty) return true;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.path != ScenePath) continue;

                if (SceneManager.sceneCount == 1)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    return true;
                }
                EditorSceneManager.CloseScene(open, true);
                return false;
            }
            return false;
        }

        // --- 材質 ---

        private sealed class Materials
        {
            public readonly Material Soul;
            public readonly Material Water;
            public readonly Material Stone;
            public readonly Material Silhouette;
            public readonly Material Mote;
            public readonly Material Flame;
            public readonly Material Portal;
            public readonly Material Sky;

            public Materials()
            {
                Soul = ShaderMaterial("Soul", "TpsDungeon/Soul", null);
                Mote = ShaderMaterial("Mote", "TpsDungeon/Afterlife Mote", mat => mat.SetColor("_Color", new Color(0.5f, 1.0f, 1.3f, 0.8f)));
                Flame = ShaderMaterial("CandleFlame", "TpsDungeon/Afterlife Mote", mat =>
                {
                    mat.SetColor("_Color", new Color(0.7f, 1.5f, 2.2f, 1f));
                    mat.SetFloat("_Softness", 1.4f);
                });
                Portal = ShaderMaterial("Portal", "TpsDungeon/Afterlife Portal", null);
                Sky = ShaderMaterial("Sky", "TpsDungeon/Afterlife Sky", mat => mat.SetColor("_HorizonColor", FogColor));

                // 黒い鏡のような水。光沢を強くして、魂や火の光が水面ににじむように。
                Water = LitMaterial("Water", mat =>
                {
                    mat.SetColor("_BaseColor", new Color(0.02f, 0.035f, 0.045f));
                    mat.SetFloat("_Smoothness", 0.97f);
                    mat.SetFloat("_Metallic", 0f);
                });

                // 塔の石を、暗い青灰に寄せる。
                Stone = LitMaterial("Stone", mat =>
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>(StoneSourcePath);
                    if (source != null) mat.CopyPropertiesFromMaterial(source);
                    mat.SetColor("_BaseColor", new Color(0.32f, 0.38f, 0.42f));
                    mat.SetFloat("_Smoothness", 0.35f);
                });

                // 逆さの城の影。霧に溶けかける暗い単色で、光を受けない。
                Silhouette = UnlitMaterial("CastleSilhouette", new Color(0.2f, 0.32f, 0.38f));
            }

            private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");

            private static Material Create(string name, Shader shader, System.Action<Material> setup)
            {
                var material = Load(name);
                if (material != null) return material;
                if (shader == null) return null;

                material = new Material(shader) { name = name };
                setup?.Invoke(material);
                AssetDatabase.CreateAsset(material, $"{MaterialsFolder}/{name}.mat");
                return material;
            }

            private static Material ShaderMaterial(string name, string shaderName, System.Action<Material> setup) => Create(name, Shader.Find(shaderName), setup);

            private static Material LitMaterial(string name, System.Action<Material> setup) => Create(name, Shader.Find("Universal Render Pipeline/Lit"), setup);

            private static Material UnlitMaterial(string name, Color color) =>
                Create(name, Shader.Find("Universal Render Pipeline/Unlit"), mat => mat.SetColor("_BaseColor", color));
        }

        // --- 光・空・霧・Volume ---

        private static void CreateLighting(Materials m)
        {
            // 真上近くからの、ごく弱い月明かり。形がかろうじて分かるくらい。
            var light = new GameObject("Directional Light");
            var component = light.AddComponent<Light>();
            component.type = LightType.Directional;
            component.intensity = 0.35f;
            component.color = new Color(0.55f, 0.75f, 0.9f);
            component.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(70f, 20f, 0f);

            RenderSettings.skybox = m.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.19f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.12f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.01f, 0.015f, 0.02f);

            // 既定の映り込みは使わない（焼いていないシーンでは明るい既定の空が映り、黒い水に白い地平が出る）。
            // 水面の映り込みは、場所全体を囲む反射プローブ（BuildReflection）で、このシーンの空・逆さの城・燭台を映す。
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.011f;
        }

        /// <summary>寒色・低い彩度・周りを暗く・光をにじませる。カメラの post-processing は FloorPostProcessing がこのシーンにいる間だけ入れる。</summary>
        private static void CreateVolume()
        {
            var holder = new GameObject("Global Volume");
            var volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = LoadOrCreateVolumeProfile();
            holder.AddComponent<FloorPostProcessing>();
        }

        private static VolumeProfile LoadOrCreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(0.7f, 0.95f, 1f));

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>();
            color.saturation.Override(-35f);
            color.colorFilter.Override(new Color(0.8f, 0.95f, 1f));
            color.postExposure.Override(0.2f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.6f);
            vignette.color.Override(new Color(0f, 0.02f, 0.03f));

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // --- 地形 ---

        /// <summary>浅い水。見た目は大きな平面で、当たりはその少し下の床（水の中も歩ける）。</summary>
        private static void BuildWater(Transform walkable, Materials m)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(walkable.parent, false);
            // 霧の向こうまで続いて見えるよう、歩ける範囲よりずっと広く。Plane は 1 倍で 10 m。
            water.transform.localPosition = new Vector3(0f, 0f, 12f);
            water.transform.localScale = new Vector3(60f, 1f, 60f);
            water.GetComponent<MeshRenderer>().sharedMaterial = m.Water;
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var floor = new GameObject("WaterFloor");
            floor.transform.SetParent(walkable, false);
            floor.transform.localPosition = new Vector3((AreaMinX + AreaMaxX) * 0.5f, -0.25f, (AreaMinZ + AreaMaxZ) * 0.5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(AreaMaxX - AreaMinX, 0.5f, AreaMaxZ - AreaMinZ);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>3 か所の広い足場と、そのあいだの飛び石。</summary>
        private static void BuildStones(Transform walkable, Materials m)
        {
            Transform group = Group(walkable, "Stones");
            Stone(group, "Landing", m, Landing, new Vector2(6f, 6f), 0f);
            Stone(group, "AltarFloor", m, AltarCenter, new Vector2(9f, 9f), 0f);
            Stone(group, "PortalFloor", m, PortalCenter, new Vector2(7.5f, 7.5f), 45f);

            // 飛び石。少しずつ向きと大きさをずらして、並べた感じを消す。
            var random = new System.Random(20261021);
            Vector3[] path =
            {
                new Vector3(-0.6f, 0f, 4.2f), new Vector3(-1.6f, 0f, 6.0f),
                new Vector3(-0.8f, 0f, 15.9f), new Vector3(0.3f, 0f, 17.6f),
                new Vector3(2.4f, 0f, 9.5f), new Vector3(3.2f, 0f, 12.8f), new Vector3(2.2f, 0f, 15.8f),
            };
            for (int i = 0; i < path.Length; i++)
            {
                float size = 1.1f + (float)random.NextDouble() * 0.6f;
                Stone(group, $"Step_{i}", m, path[i], new Vector2(size, size * (0.8f + (float)random.NextDouble() * 0.3f)), (float)random.NextDouble() * 90f);
            }
        }

        private static GameObject Stone(Transform parent, string name, Materials m, Vector3 center, Vector2 size, float yaw)
        {
            const float thickness = 0.6f;
            GameObject stone = SurfaceSceneBuilder.Box(parent, name, m.Stone, center + Vector3.up * (StoneTop - thickness * 0.5f), new Vector3(size.x, thickness, size.y));
            stone.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return stone;
        }

        /// <summary>燭台の輪と、真ん中の強化の台。火は粒子と弱い光で、魂と同じ青白い色。</summary>
        private static void BuildAltar(Transform world, Transform walkable, Materials m)
        {
            Transform group = Group(world, "UpgradeAltar");

            GameObject altar = SurfaceSceneBuilder.Box(walkable, "Altar", m.Stone, AltarCenter + Vector3.up * (StoneTop + 0.5f), new Vector3(1.4f, 1f, 1.4f));
            altar.AddComponent<UpgradeAltar>();
            Flame(group, "AltarFlame", m, AltarCenter + Vector3.up * (StoneTop + 1.15f), 1.6f, 0.35f);

            for (int i = 0; i < Candles; i++)
            {
                float angle = (i + 0.5f) / Candles * Mathf.PI * 2f;
                Vector3 at = AltarCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CandleRadius;
                float height = 1.0f + (i % 3) * 0.25f;
                SurfaceSceneBuilder.Box(walkable, $"Candle_{i}", m.Stone, at + Vector3.up * (StoneTop + height * 0.5f), new Vector3(0.3f, height, 0.3f));
                Flame(group, $"CandleFlame_{i}", m, at + Vector3.up * (StoneTop + height + 0.12f), 0.8f, 0.18f);
            }
        }

        private static void Flame(Transform parent, string name, Materials m, Vector3 at, float lightIntensity, float size)
        {
            var flame = new GameObject(name);
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = at;

            var light = flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.85f, 1f);
            light.intensity = lightIntensity;
            light.range = 4.5f;
            light.shadows = LightShadows.None;

            ParticleSystem particles = Particles(flame, m.Flame);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.maxParticles = 40;
            var emission = particles.emission;
            emission.rateOverTime = 14f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.03f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        /// <summary>水面の転生の渦。入る（インタラクトする）と地上へ戻る。</summary>
        private static void BuildPortal(Transform world, Materials m)
        {
            Transform group = Group(world, "RebirthPortal");
            group.localPosition = PortalCenter + Vector3.up * (StoneTop + 0.02f);

            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Vortex";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(group, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(4f, 4f, 1f);
            var renderer = disc.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m.Portal;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var light = group.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.95f, 1f);
            light.intensity = 3f;
            light.range = 9f;
            light.shadows = LightShadows.None;

            // 上へ昇っていく粒。
            ParticleSystem particles = Particles(group.gameObject, m.Mote);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.maxParticles = 120;
            var emission = particles.emission;
            emission.rateOverTime = 22f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOverLifetime(particles);

            // 渦に照準を合わせやすいよう、渦の上に背の高いトリガーの箱を置く（足場の NavMesh には入れない）。
            var trigger = group.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(3.2f, 2f, 3.2f);

            SurfaceSceneBuilder.AddGate(group.gameObject, SceneTravel.SurfaceScene, true, "転生する", "", "転生の渦",
                "ここへ沈むと、城の前で新しい体に目覚める。");
        }

        /// <summary>
        /// 地上の城の塔のメッシュ（土台の飾り・各層）を上下逆さにして、頭上の遠く（転生の渦の向こう）に垂らす。
        /// 当たりも影も無い。塔のメッシュがまだ無ければ（地上を作っていなければ）置かない。
        /// </summary>
        private static string BuildInvertedCastle(Transform world, Materials m)
        {
            var meshes = new List<Mesh>();
            foreach (string name in new[] { "Tower_Base", "Tower_L1", "Tower_L2", "Tower_L3", "Tower_L4", "Tower_L5" })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{TowerMeshFolder}/{name}.asset");
                if (mesh != null) meshes.Add(mesh);
            }
            if (meshes.Count == 0) return "逆さの城は地上の塔のメッシュが無いので置いていない";

            Bounds bounds = meshes[0].bounds;
            foreach (Mesh mesh in meshes) bounds.Encapsulate(mesh.bounds);

            // 上下を逆さにして（てっぺんが下）縮める。いちばん低い所が InvertedCastleLowest に来るよう持ち上げる。
            // 地上と同じく正面（大門の側）が -Z を向くので、こちらを向いて垂れ下がる。
            var castle = new GameObject("InvertedCastle").transform;
            castle.SetParent(world, false);
            castle.localScale = new Vector3(InvertedCastleScale, -InvertedCastleScale, InvertedCastleScale);
            float lift = InvertedCastleLowest + bounds.max.y * InvertedCastleScale;
            castle.localPosition = new Vector3(-bounds.center.x * InvertedCastleScale, lift, InvertedCastleDistance - bounds.center.z * InvertedCastleScale);

            foreach (Mesh mesh in meshes)
            {
                var part = new GameObject(mesh.name);
                part.transform.SetParent(castle, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = m.Silhouette;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
            }
            return $"頭上に逆さの城（塔のメッシュ {meshes.Count} 個）";
        }

        /// <summary>
        /// 水面に空・逆さの城・燭台を映す反射プローブ。生成のときにキューブマップへ焼いて、そのまま当てる（Custom）。
        /// 実行中に撮るプローブは Quality の設定で切ってあり、シーン全体のライティングを焼くほどでもないので。パーティーを置く前に焼く（映り込みに普通の姿が入らないように）。
        /// </summary>
        private static void BuildReflection(Transform world)
        {
            var holder = new GameObject("Reflection Probe");
            holder.transform.SetParent(world, false);
            holder.transform.localPosition = new Vector3(0f, 1.5f, 12f);

            var probe = holder.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.size = new Vector3(400f, 200f, 400f);
            probe.resolution = 256;
            probe.farClipPlane = 400f;
            probe.hdr = true;

            EnsureFolder(System.IO.Path.GetDirectoryName(ReflectionPath)?.Replace('\\', '/'));
            Physics.SyncTransforms();
            if (!Lightmapping.BakeReflectionProbe(probe, ReflectionPath)) return;
            AssetDatabase.ImportAsset(ReflectionPath);
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(ReflectionPath);
        }

        /// <summary>場所全体に、ゆっくり沈んでいく光の粒。</summary>
        private static void BuildMotes(Transform world, Materials m)
        {
            var holder = new GameObject("SinkingMotes");
            holder.transform.SetParent(world, false);
            holder.transform.localPosition = new Vector3(0f, 14f, 12f);

            ParticleSystem particles = Particles(holder, m.Mote);
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.maxParticles = 600;
            main.prewarm = true;
            var emission = particles.emission;
            emission.rateOverTime = 24f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 2f, 60f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.75f, -0.45f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            FadeOverLifetime(particles);
        }

        private static ParticleSystem Particles(GameObject holder, Material material)
        {
            var particles = holder.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void FadeOverLifetime(ParticleSystem particles)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        /// <summary>歩ける範囲の 4 辺のすぐ外に見えない壁（SurfaceSceneBuilder.BuildBounds と同じ作り。Ignore Raycast で、NavMesh を焼く範囲の外）。</summary>
        private static void BuildBounds()
        {
            var root = new GameObject("[Bounds]").transform;
            float width = AreaMaxX - AreaMinX + BoundsThickness * 2f;
            float depth = AreaMaxZ - AreaMinZ;
            float centerX = (AreaMinX + AreaMaxX) * 0.5f;
            float centerZ = (AreaMinZ + AreaMaxZ) * 0.5f;
            float y = BoundsHeight * 0.5f;
            float half = BoundsThickness * 0.5f;

            InvisibleWall(root, "Bound_S", new Vector3(centerX, y, AreaMinZ - half), new Vector3(width, BoundsHeight, BoundsThickness));
            InvisibleWall(root, "Bound_N", new Vector3(centerX, y, AreaMaxZ + half), new Vector3(width, BoundsHeight, BoundsThickness));
            InvisibleWall(root, "Bound_W", new Vector3(AreaMinX - half, y, centerZ), new Vector3(BoundsThickness, BoundsHeight, depth));
            InvisibleWall(root, "Bound_E", new Vector3(AreaMaxX + half, y, centerZ), new Vector3(BoundsThickness, BoundsHeight, depth));
        }

        private static void InvisibleWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name) { layer = 2 };
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = center;
            wall.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>着地点。転生の渦を向いて立つ。</summary>
        private static Transform CreateSpawn(Transform world)
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(world, false);
            spawn.transform.SetLocalPositionAndRotation(Landing + new Vector3(0f, StoneTop + 0.05f, -1f), Quaternion.identity);
            spawn.AddComponent<PlayerSpawnPoint>();
            return spawn.transform;
        }

        // --- NavMesh・Build Settings ---

        /// <summary>水の床・足場・台と燭台（Walkable の下）の当たりから NavMesh を焼き、アセットに書き出してシーンから参照させる。</summary>
        private static void BakeNavMesh(GameObject walkable)
        {
            var surface = walkable.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = BreakableLayer.Exclude(AllyLayer.Exclude(~0));
            surface.buildHeightMesh = true;

            Physics.SyncTransforms();
            surface.BuildNavMesh();

            EnsureFolder(System.IO.Path.GetDirectoryName(NavMeshPath)?.Replace('\\', '/'));
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>Build Settings に有効で足す（先頭の地上はそのまま）。</summary>
        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            EnsureFolder(System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/'));
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }
    }
}
