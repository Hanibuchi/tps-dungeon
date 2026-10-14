using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string VolumeProfilePath = "Assets/_Project/Settings/Map/Floor_VolumeProfile.asset";
        private const string VolumeName = "[Global Volume]";

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
                PlaceGlobalVolume();
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

        /// <summary>
        /// アクティブなシーンに、階の明るさを整える Global Volume を置く（あれば profile を差し直すだけ）。
        /// 天井が日の光を遮るので、室内は環境光だけで暗い。暗部を持ち上げて、窓の外は飛ばさずに室内を見えるようにする。
        /// 効かせるにはカメラの post-processing が要るので、階にいる間だけ有効にする FloorPostProcessing を一緒に付ける（地上の見た目は変えない）。
        /// </summary>
        public static Volume PlaceGlobalVolume()
        {
            var scene = SceneManager.GetActiveScene();
            Volume volume = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == VolumeName && root.TryGetComponent(out volume)) break;
            }

            if (volume == null) volume = new GameObject(VolumeName).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = LoadOrCreateVolumeProfile();
            if (!volume.TryGetComponent(out FloorPostProcessing _)) volume.gameObject.AddComponent<FloorPostProcessing>();
            EditorUtility.SetDirty(volume);
            return volume;
        }

        /// <summary>階の Volume の profile。無ければ作る。値を手で直したらそちらを使う（上書きしない）。</summary>
        private static VolumeProfile LoadOrCreateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            // 暗部と中間を持ち上げる（w が明るさのずらし）。明るい所はそのままなので、窓の外の景色は飛ばない。
            var tones = profile.Add<ShadowsMidtonesHighlights>();
            tones.shadows.Override(new Vector4(1f, 1f, 1f, 0.35f));
            tones.midtones.Override(new Vector4(1f, 1f, 1f, 0.15f));

            // 窓から入る日向が白く飛ばないよう、明るい所をなだらかに丸める。
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.3f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
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
