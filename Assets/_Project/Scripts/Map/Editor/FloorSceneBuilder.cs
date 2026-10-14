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
        private const string DefaultConfigPath = "Assets/_Project/Settings/Map/Floor_L1_Config.asset";

        [MenuItem("Tools/TPS Dungeon/Floor_1_1 シーンを作る")]
        public static void CreateFloor11()
        {
            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(DefaultConfigPath);
            if (config == null)
            {
                Debug.LogError($"{DefaultConfigPath} が無い。先に「プレースホルダのマップ一式を生成」を実行すること。");
                return;
            }

            CreateFloorScene(config, $"{ScenesFolder}/Floor_1_1.unity");
        }

        public static void CreateFloorScene(FloorConfig config, string scenePath)
        {
            // 追加ロードで作って保存後に閉じる。いま開いているシーンの未保存の変更を巻き込まないため。
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                CreateLighting();
                var bootstrap = CreateFloorRoot(config);
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

        private static FloorBootstrap CreateFloorRoot(FloorConfig config)
        {
            var root = new GameObject("[Floor]");
            root.AddComponent<FloorBuilder>();
            var bootstrap = root.AddComponent<FloorBootstrap>();

            // private [SerializeField] なので SerializedObject 経由で差し込む。
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
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
