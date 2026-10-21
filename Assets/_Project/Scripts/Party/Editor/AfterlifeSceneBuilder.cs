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
    /// - どこまでも続く黒い水面に、平たい石の足場（<see cref="Platforms"/>）。南の着地点から北へ渡ると燭台の輪（強化）、そこから東へ渡ると転生の渦。
    ///   歩けるのは足場の上だけで、縁に淡い光の線を引き、縁の外に見えない壁を立てる。
    /// - 足場には道しるべの淡い光の粒を並べ、光の波を行き先へ流す。強化の台から渦までの後半は、台に近づくまで隠す（<see cref="AfterlifeGuide"/>）。
    /// - 地図（<see cref="SurfaceMapSource"/>）を真上から撮って持たせ、強化の台と転生の渦を印で出す。左上の札は「死後の世界」。
    /// - 頭上の遠くに、地上の城の塔のメッシュ（Tower_*）を上下逆さにして垂らす。上ほど太い城が、ここでは下ほど太い。
    /// - 寒色・低い彩度で暗く、主な光は魂（SoulForm が一人ずつに付ける光）と燭台の青白い火。光の粒がゆっくり沈んでいく。
    /// - 材質と Volume の profile は無いときだけ作る（手で調節した値を上書きしない）。
    /// - 直接 Play して確かめられるよう、地上と同じパーティー一式（[Party Rig]）も置く。地上から来たときは GameFlow がこちらを消す。仲間は SoulForm が消す。
    /// </summary>
    public static class AfterlifeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Afterlife.unity";
        private const string NavMeshPath = "Assets/_Project/Scenes/Afterlife/NavMesh-Afterlife.asset";
        private const string ReflectionPath = "Assets/_Project/Scenes/Afterlife/ReflectionProbe-Afterlife.exr";
        private const string MaterialsFolder = "Assets/_Project/Materials/Afterlife";
        private const string VolumeProfilePath = "Assets/_Project/Materials/Afterlife/Afterlife_VolumeProfile.asset";
        private const string TowerMeshFolder = "Assets/_Project/Scenes/Surface";
        private const string MapImagePath = "Assets/_Project/UI/Hud/AfterlifeMap.png";
        private const string PlaceName = "死後の世界";
        private const string StoneSourcePath = "Assets/ThirdParty/Material/YughuesFreePavementsMaterials/Materials/M_YFPM_JaggedSquare.mat";

        // 足場（x・z の最小と最大、メートル）。+Z が北。歩けるのはこの上だけ。どれも GridStep の倍数に揃える（縁を升目で探すので）。
        private static readonly Rect[] Platforms =
        {
            Rect.MinMaxRect(-3f, -3f, 3f, 3f),     // 着地点（南）
            Rect.MinMaxRect(-1.5f, 3f, 1.5f, 6f),  // 北への渡り
            Rect.MinMaxRect(-5f, 6f, 5f, 16f),     // 強化の台
            Rect.MinMaxRect(5f, 9.5f, 10f, 12.5f), // 東への渡り
            Rect.MinMaxRect(10f, 7f, 18f, 15f),    // 転生の渦
        };
        private const float GridStep = 0.5f;
        private const float WallHeight = 6f;
        private const float WallThickness = 1f;
        private const float EdgeGlowWidth = 0.2f;

        // 3 か所の足場の真ん中。
        private static readonly Vector3 Landing = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AltarCenter = new Vector3(0f, 0f, 11f);
        private static readonly Vector3 PortalCenter = new Vector3(14f, 0f, 11f);

        // 道しるべ。前半は着地点から強化の台の手前まで北へ、後半は台の脇から渦の手前まで東へ。
        private static readonly Vector3 PathToAltarStart = new Vector3(0f, 0f, -0.5f);
        private static readonly Vector3 PathToAltarEnd = new Vector3(0f, 0f, 9f);
        private static readonly Vector3 PathToPortalStart = new Vector3(1.5f, 0f, 11f);
        private static readonly Vector3 PathToPortalEnd = new Vector3(11.6f, 0f, 11f);
        private const float PathSpacing = 1.1f;
        private const float PathDotSize = 0.34f;

        // 燭台の向き（度。0 が東、90 が北）。南（着地点から来る道）と東（渦へ出る道）を空ける。
        private static readonly float[] CandleAngles = { 45f, 90f, 135f, 180f, 225f, 315f };

        // 足場は水面（y = 0）から少しだけ出る。
        private const float StoneTop = 0.12f;
        private const float CandleRadius = 3.2f;
        // 強化の台のまわりの明るさ。台と燭台の形が見分けられるくらい。
        private const float AltarFlameIntensity = 2.4f;
        private const float CandleFlameIntensity = 1.5f;
        private const float AltarOverheadIntensity = 3.5f;

        // 逆さの城。いちばん低い所（地上の城のてっぺん）をこの高さに、この奥に置く。
        private const float InvertedCastleLowest = 6f;
        private const float InvertedCastleDistance = 110f;
        private const float InvertedCastleScale = 0.7f;

        private static readonly Color WaterMapColor = new Color(0.02f, 0.04f, 0.05f);
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
                BuildWater(world, m);
                BuildStones(walkable, m);
                BuildEdges(world, m);
                Transform altar = BuildAltar(world, walkable, m);
                BuildPath(world, altar, m);
                BuildPortal(world, m);
                castle = BuildInvertedCastle(world, m);
                BuildReflection(world);
                BuildMotes(world, m);
                Transform spawn = CreateSpawn(world);
                CaptureMap(world.gameObject);

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
            return $"{ScenePath} を作った（南の着地点 → 北の強化の台 → その東の転生の渦（地上へ）、{castle}）。";
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
            public readonly Material Edge;
            public readonly Material PathToAltar;
            public readonly Material PathToPortal;
            public readonly Material Beam;

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
                Edge = ShaderMaterial("EdgeGlow", "TpsDungeon/Afterlife Edge Glow", null);

                // 道しるべは区間ごとに、道の向きと、前の区間から続く距離を入れる（光の波が折れ目でつながるように）。
                PathToAltar = ShaderMaterial("PathToAltar", "TpsDungeon/Afterlife Path Glow", mat =>
                {
                    mat.SetVector("_Axis", new Vector4(0f, 0f, 1f, 0f));
                    mat.SetFloat("_Offset", -PathToAltarStart.z);
                });
                float firstLength = Vector3.Distance(PathToAltarStart, PathToAltarEnd) + Vector3.Distance(PathToAltarEnd, PathToPortalStart);
                PathToPortal = ShaderMaterial("PathToPortal", "TpsDungeon/Afterlife Path Glow", mat =>
                {
                    mat.SetVector("_Axis", new Vector4(1f, 0f, 0f, 0f));
                    mat.SetFloat("_Offset", firstLength - PathToPortalStart.x);
                });

                // 転生の渦の真ん中に立つ光の柱。魂と同じシェーダーで、もっと薄く速く流れる。
                Beam = ShaderMaterial("PortalBeam", "TpsDungeon/Soul", mat =>
                {
                    mat.SetColor("_RimColor", new Color(0.4f, 0.95f, 1.4f, 1f));
                    mat.SetColor("_BodyColor", new Color(0.2f, 0.5f, 0.7f, 0.08f));
                    mat.SetFloat("_RimPower", 2f);
                    mat.SetFloat("_FlowSpeed", 0.9f);
                    mat.SetFloat("_NoiseStrength", 0.7f);
                });

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

        /// <summary>水。見た目だけの大きな平面で、当たりは無い（足場の外へは縁の見えない壁で出られない）。</summary>
        private static void BuildWater(Transform world, Materials m)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(world, false);
            // 霧の向こうまで続いて見えるよう、足場よりずっと広く。Plane は 1 倍で 10 m。
            water.transform.localPosition = new Vector3(7f, 0f, 8f);
            water.transform.localScale = new Vector3(60f, 1f, 60f);
            water.GetComponent<MeshRenderer>().sharedMaterial = m.Water;
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>足場（<see cref="Platforms"/>）を 1 つずつ石の板にする。上面は水面から少しだけ出る。</summary>
        private static void BuildStones(Transform walkable, Materials m)
        {
            const float thickness = 0.6f;
            Transform group = Group(walkable, "Stones");
            string[] names = { "Landing", "NorthCauseway", "AltarFloor", "EastCauseway", "PortalFloor" };
            for (int i = 0; i < Platforms.Length; i++)
            {
                Rect r = Platforms[i];
                var center = new Vector3(r.center.x, StoneTop - thickness * 0.5f, r.center.y);
                SurfaceSceneBuilder.Box(group, i < names.Length ? names[i] : $"Platform_{i}", m.Stone, center, new Vector3(r.width, thickness, r.height));
            }
        }

        private static bool OnPlatform(float x, float z)
        {
            foreach (Rect r in Platforms)
            {
                if (x > r.xMin && x < r.xMax && z > r.yMin && z < r.yMax) return true;
            }
            return false;
        }

        /// <summary>
        /// 足場をひとつながりの形として見たときの縁（水との境目）に、淡い光の線と見えない壁を置く。
        /// 縁は GridStep の升目で探し、まっすぐ続く所は 1 本にまとめる。足場どうしがつながる所には置かない。
        /// 壁は Ignore Raycast で [Bounds] に置く（照準やカメラの壁よけに掛からず、NavMesh も焼かない）。
        /// </summary>
        private static void BuildEdges(Transform world, Materials m)
        {
            Transform glow = Group(world, "EdgeGlow");
            Transform walls = new GameObject("[Bounds]").transform;

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (Rect r in Platforms)
            {
                minX = Mathf.Min(minX, r.xMin); minZ = Mathf.Min(minZ, r.yMin);
                maxX = Mathf.Max(maxX, r.xMax); maxZ = Mathf.Max(maxZ, r.yMax);
            }
            int columns = Mathf.RoundToInt((maxX - minX) / GridStep);
            int rows = Mathf.RoundToInt((maxZ - minZ) / GridStep);
            bool Inside(int i, int j) => i >= 0 && j >= 0 && i < columns && j < rows
                && OnPlatform(minX + (i + 0.5f) * GridStep, minZ + (j + 0.5f) * GridStep);

            int count = 0;
            // 東西に走る縁（升目の行の境目 j）。外側が南（-1）か北（+1）か。
            for (int j = 0; j <= rows; j++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int start = -1;
                    for (int i = 0; i <= columns; i++)
                    {
                        // 内側の升目と外側の升目。side = -1 なら外が南（j - 1）で内が北（j）。
                        bool edge = i < columns && (side < 0 ? Inside(i, j) && !Inside(i, j - 1) : Inside(i, j - 1) && !Inside(i, j));
                        if (edge && start < 0) start = i;
                        if (!edge && start >= 0)
                        {
                            float z = minZ + j * GridStep;
                            Edge(glow, walls, m, new Vector3(minX + start * GridStep, 0f, z), new Vector3(minX + i * GridStep, 0f, z), new Vector3(0f, 0f, side), count++);
                            start = -1;
                        }
                    }
                }
            }
            // 南北に走る縁（升目の列の境目 i）。外側が西（-1）か東（+1）か。
            for (int i = 0; i <= columns; i++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int start = -1;
                    for (int j = 0; j <= rows; j++)
                    {
                        bool edge = j < rows && (side < 0 ? Inside(i, j) && !Inside(i - 1, j) : Inside(i - 1, j) && !Inside(i, j));
                        if (edge && start < 0) start = j;
                        if (!edge && start >= 0)
                        {
                            float x = minX + i * GridStep;
                            Edge(glow, walls, m, new Vector3(x, 0f, minZ + start * GridStep), new Vector3(x, 0f, minZ + j * GridStep), new Vector3(side, 0f, 0f), count++);
                            start = -1;
                        }
                    }
                }
            }
        }

        /// <summary>from から to までのまっすぐな縁。outward は水の側。足場の上に光の線、水の側に見えない壁。</summary>
        private static void Edge(Transform glow, Transform walls, Materials m, Vector3 from, Vector3 to, Vector3 outward, int index)
        {
            Vector3 mid = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to);
            bool alongX = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.z - from.z);

            var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = $"Edge_{index}";
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(glow, false);
            line.transform.localPosition = mid - outward * (EdgeGlowWidth * 0.5f) + Vector3.up * (StoneTop + 0.005f);
            // Quad の X が線の向き、Y が幅の向き。寝かせて上を向ける。
            line.transform.localRotation = Quaternion.Euler(90f, alongX ? 0f : 90f, 0f);
            line.transform.localScale = new Vector3(length, EdgeGlowWidth, 1f);
            var renderer = line.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m.Edge;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var wall = new GameObject($"Wall_{index}") { layer = 2 };
            wall.transform.SetParent(walls, false);
            wall.transform.localPosition = mid + outward * (WallThickness * 0.5f) + Vector3.up * (WallHeight * 0.5f);
            wall.AddComponent<BoxCollider>().size = alongX ? new Vector3(length, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, length);
            GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// 道しるべ。足場に淡い光の粒を並べ、光の波を行き先へ流す（TpsDungeon/Afterlife Path Glow）。
        /// 前半（着地点 → 強化の台）はいつも見え、後半（強化の台 → 転生の渦）は台に近づくまで隠す（<see cref="AfterlifeGuide"/>）。
        /// 粒は物体の原点で波の位置を測るので、静的なまとめ描き（Static Batching）にしない。
        /// </summary>
        private static void BuildPath(Transform world, Transform altar, Materials m)
        {
            Transform group = Group(world, "PathGuide");
            Transform toAltar = Group(group, "ToAltar");
            Transform toPortal = Group(group, "ToPortal");
            Dots(toAltar, m.PathToAltar, PathToAltarStart, PathToAltarEnd);
            Dots(toPortal, m.PathToPortal, PathToPortalStart, PathToPortalEnd);

            var guide = new SerializedObject(group.gameObject.AddComponent<AfterlifeGuide>());
            guide.FindProperty("altar").objectReferenceValue = altar;
            guide.FindProperty("laterPath").objectReferenceValue = toPortal.gameObject;
            guide.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Dots(Transform parent, Material material, Vector3 from, Vector3 to)
        {
            int count = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(from, to) / PathSpacing) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = count == 1 ? from : Vector3.Lerp(from, to, i / (float)(count - 1));
                var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                dot.name = $"Dot_{i}";
                Object.DestroyImmediate(dot.GetComponent<Collider>());
                dot.transform.SetParent(parent, false);
                dot.transform.localPosition = at + Vector3.up * (StoneTop + 0.01f);
                dot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                dot.transform.localScale = new Vector3(PathDotSize, PathDotSize, 1f);
                var renderer = dot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// 燭台の輪と、真ん中の強化の台（地図に印を出す）。火は粒子と光で、魂と同じ青白い色。台を返す。
        /// 暗い中でも台と燭台の形が分かるよう、輪の真上から足場を広く照らす光も置く。
        /// </summary>
        private static Transform BuildAltar(Transform world, Transform walkable, Materials m)
        {
            Transform group = Group(world, "UpgradeAltar");

            GameObject altar = SurfaceSceneBuilder.Box(walkable, "Altar", m.Stone, AltarCenter + Vector3.up * (StoneTop + 0.5f), new Vector3(1.4f, 1f, 1.4f));
            altar.AddComponent<UpgradeAltar>();
            var marker = altar.AddComponent<MapMarker>();
            marker.Icon = MapIcon.Upgrade;
            marker.Label = "魂の燭台";
            Flame(group, "AltarFlame", m, AltarCenter + Vector3.up * (StoneTop + 1.15f), AltarFlameIntensity, 0.35f);

            // 輪の真上の光。燭台の火だけでは柱の根元と台の側面が闇に沈むので、輪全体を淡く照らす。
            var overhead = new GameObject("AltarOverheadLight");
            overhead.transform.SetParent(group, false);
            overhead.transform.localPosition = AltarCenter + Vector3.up * 4.5f;
            var light = overhead.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.82f, 1f);
            light.intensity = AltarOverheadIntensity;
            light.range = 10f;
            light.shadows = LightShadows.Soft;

            for (int i = 0; i < CandleAngles.Length; i++)
            {
                float angle = CandleAngles[i] * Mathf.Deg2Rad;
                Vector3 at = AltarCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CandleRadius;
                float height = 1.0f + (i % 3) * 0.25f;
                SurfaceSceneBuilder.Box(walkable, $"Candle_{i}", m.Stone, at + Vector3.up * (StoneTop + height * 0.5f), new Vector3(0.3f, height, 0.3f));
                Flame(group, $"CandleFlame_{i}", m, at + Vector3.up * (StoneTop + height + 0.12f), CandleFlameIntensity, 0.18f);
            }
            return altar.transform;
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
            light.range = 5.5f;
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

        /// <summary>
        /// 足場の上の転生の渦と、その真ん中に立つ光の柱。柱をインタラクトすると地上へ戻る。
        /// 柱は細く当たりのある円柱にして、渦のどこに立っても柱に照準を合わせられるようにする（中に入れる大きな判定だと、入った所で照準が当たらない）。
        /// </summary>
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

            // 光の柱（半径 0.45 m・高さ 3.2 m）。円柱の当たり（CapsuleCollider）はそのまま使う。
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            beam.transform.SetParent(group, false);
            beam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            beam.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);
            var beamRenderer = beam.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = m.Beam;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;

            SurfaceSceneBuilder.AddGate(beam, SceneTravel.SurfaceScene, true, "転生する", "", "転生の渦",
                "ここへ沈むと、城の前で新しい体に目覚める。", MapIcon.Rebirth, "転生の渦");
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
            holder.transform.localPosition = new Vector3(7f, 1.5f, 8f);

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
            holder.transform.localPosition = new Vector3(7f, 14f, 8f);

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

        /// <summary>着地点。北（強化の台）を向いて立つ。</summary>
        private static Transform CreateSpawn(Transform world)
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(world, false);
            spawn.transform.SetLocalPositionAndRotation(Landing + new Vector3(0f, StoneTop + 0.05f, -1.5f), Quaternion.identity);
            spawn.AddComponent<PlayerSpawnPoint>();
            return spawn.transform;
        }

        /// <summary>
        /// 足場のまわりを真上から撮って地図の絵にし、[Afterlife] の地図の元（SurfaceMapSource）に差す。
        /// 頭上の逆さの城・沈む光の粒・パーティーは撮る間だけ隠す。城の土台の四角は無い。
        /// </summary>
        private static void CaptureMap(GameObject world)
        {
            const float margin = 4f;
            Rect area = Platforms[0];
            foreach (Rect r in Platforms) area = Rect.MinMaxRect(Mathf.Min(area.xMin, r.xMin), Mathf.Min(area.yMin, r.yMin), Mathf.Max(area.xMax, r.xMax), Mathf.Max(area.yMax, r.yMax));
            area = Rect.MinMaxRect(area.xMin - margin, area.yMin - margin, area.xMax + margin, area.yMax + margin);

            SurfaceMapCapture.Capture(world, area, MapImagePath, Rect.zero, PlaceName, WaterMapColor,
                new[] { "InvertedCastle", "SinkingMotes" }, new[] { "[Party Rig]" });
        }

        // --- NavMesh・Build Settings ---

        /// <summary>足場・台と燭台（Walkable の下）の当たりから NavMesh を焼き、アセットに書き出してシーンから参照させる。</summary>
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
