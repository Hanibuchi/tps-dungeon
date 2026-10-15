using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.DebugTools;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// 階 1 つ分のシーンを組み立てる。各階は自分の FloorConfig を持つ同じ形のシーンなので、
    /// 他の階を足すときもこのスクリプトから作れる。
    /// </summary>
    public static class FloorSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        public const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        // L1 のランダムな階のシーンは以前 Floor_1_1 という名前だった。中身（GUID）を保ったまま名前を変える。
        private const string LegacyFirstFloorPath = ScenesFolder + "/Floor_1_1.unity";

        /// <summary>
        /// 道順（DungeonRoute）に載っている階のシーン（L{n}_Random・L{n}_Boss）を全部作り直す。
        /// [Floor] の FloorBootstrap に道順とその層の FloorConfig を差し、デバッグ用の入力（FloorDebugInput）を付ける。
        /// 窓の外の景色は「階の外の景色を置く（全層）」で置く。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/階のシーンを作る（全層）")]
        public static void CreateAllFloorScenes()
        {
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route == null)
            {
                Debug.LogError($"{RoutePath} が無い。先に「ダンジョンの道順を作る」を実行すること。");
                return;
            }

            RenameLegacyFirstFloor(route);

            int made = 0;
            foreach (var layer in route.Layers)
            {
                if (layer.randomFloorCount > 0 && !string.IsNullOrEmpty(layer.randomScene) && layer.randomConfig != null)
                {
                    CreateFloorScene(layer.randomConfig, $"{ScenesFolder}/{layer.randomScene}.unity", route);
                    made++;
                }
                if (layer.HasBoss && layer.bossConfig != null)
                {
                    CreateFloorScene(layer.bossConfig, $"{ScenesFolder}/{layer.bossScene}.unity", route);
                    made++;
                }
            }
            RemoveMissingFromBuildSettings();
            Debug.Log($"階のシーンを {made} 個作った");
        }

        /// <summary>Floor_1_1 が残っていれば、道順の最初のランダムな階のシーン名（L1_Random）に名前を変える。</summary>
        private static void RenameLegacyFirstFloor(DungeonRoute route)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyFirstFloorPath) == null || route.Layers.Count == 0) return;

            string target = $"{ScenesFolder}/{route.Layers[0].randomScene}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target) != null) return;

            string error = AssetDatabase.MoveAsset(LegacyFirstFloorPath, target);
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"{LegacyFirstFloorPath} を {target} に移せなかった: {error}");
        }

        public static void CreateFloorScene(FloorConfig config, string scenePath) => CreateFloorScene(config, scenePath, null);

        public static void CreateFloorScene(FloorConfig config, string scenePath, DungeonRoute route)
        {
            // 開いているシーンには上書きで保存できない（黙って失敗する）。開いている人の未保存の変更を消さないためにも飛ばす。
            var open = SceneManager.GetSceneByPath(scenePath);
            if (open.IsValid() && open.isLoaded)
            {
                Debug.LogError($"{scenePath} を開いているので作り直さなかった。別のシーンを開いてからもう一度実行すること。");
                return;
            }

            // 追加ロードで作って保存後に閉じる。いま開いているシーンの未保存の変更を巻き込まないため。
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                CreateLighting();
                var bootstrap = CreateFloorRoot(config, route);
                CreateDebugView(bootstrap);

                EditorSceneManager.SaveScene(scene, scenePath);
                AddToBuildSettings(scenePath);
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log($"{scenePath} を作成した（config: {config.name}）");
        }

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light");
            var component = light.AddComponent<Light>();
            component.type = LightType.Directional;
            component.intensity = 1.1f;
            component.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 俯瞰デバッグ表示では影より視認性が大事なので、環境光は明るめにしておく。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        private static FloorBootstrap CreateFloorRoot(FloorConfig config, DungeonRoute route)
        {
            var root = new GameObject("[Floor]");
            root.AddComponent<FloorBuilder>();
            var bootstrap = root.AddComponent<FloorBootstrap>();
            root.AddComponent<FloorDebugInput>();

            // private [SerializeField] なので SerializedObject 経由で差し込む。
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("route").objectReferenceValue = route;
            serialized.FindProperty("seed").intValue = 0;
            serialized.FindProperty("generateOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return bootstrap;
        }

        private static void CreateDebugView(FloorBootstrap bootstrap)
        {
            var cameraObject = new GameObject("[Debug] TopDownCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            var view = cameraObject.AddComponent<FloorDebugView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("topDownCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>名前を変えたり消したりして無くなったシーンを Build Settings から外す。</summary>
        private static void RemoveMissingFromBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(existing.path) != null) scenes.Add(existing);
            }
            if (scenes.Count != EditorBuildSettings.scenes.Length) EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddToBuildSettings(string scenePath)
        {
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == scenePath) return;
            }

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(scenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
