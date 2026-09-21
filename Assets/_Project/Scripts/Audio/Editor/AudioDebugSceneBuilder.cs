using System.Collections.Generic;
using TpsDungeon.Audio.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Audio.Editor
{
    /// <summary>
    /// 音まわりを耳で確かめるためのシーンを組み立てる。
    /// リスナーと往復エミッタを離して置き、距離減衰を目と耳の両方で追えるようにする。
    /// 生成物なので手で編集せず、構成を変えたくなったらこのファイルを直して作り直すこと。
    /// </summary>
    public static class AudioDebugSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Audio_Debug.unity";

        [MenuItem("Tools/TPS Dungeon/Audio/Generate Audio Debug Scene")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>確認用シーンを作り直して、何をしたかのログを返す。</summary>
        public static string Generate()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameAudioAssetSetup.PrefabPath) == null)
            {
                return "先に Generate Game Audio Config And Prefab を実行すること。プレハブが無い: "
                       + GameAudioAssetSetup.PrefabPath;
            }

            var bgmA = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioAssetGenerator.BgmPath);
            var bgmB = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioAssetGenerator.BgmBPath);
            var click = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioAssetGenerator.ClickPath);
            var hit = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioAssetGenerator.HitPath);
            var footstep = AssetDatabase.LoadAssetAtPath<AudioClip>(PlaceholderAudioAssetGenerator.FootstepPath);

            if (bgmA == null || bgmB == null || click == null || hit == null || footstep == null)
            {
                return "先に Generate Placeholder Audio Clips を実行すること。仮の音が揃っていない: "
                       + PlaceholderAudioAssetGenerator.OutputFolder;
            }

            // 追加ロードで作って保存後に閉じる。いま開いているシーンの未保存の変更を巻き込まないため。
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                CreateLighting();
                CreateCamera();

                Transform listener = CreateListener();
                Transform emitter = CreateEmitter();
                CreateDebugView(listener, emitter, bgmA, bgmB, click, hit, footstep);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            return "音の確認用シーンを作り直した: " + ScenePath
                   + "\n  Play して数字キーを押すと BGM・SE・スナップショットを鳴らし分けられる。"
                   + "\n  鳴らないときはまず Game ビューをクリックしてフォーカスを入れること。";
        }

        private static void CreateLighting()
        {
            var light = new GameObject("Directional Light");
            var component = light.AddComponent<Light>();
            component.type = LightType.Directional;
            component.intensity = 1.1f;
            component.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // マーカーが黒く潰れると位置関係が読めないので、環境光は明るめにしておく。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        /// <summary>真上から見下ろす。原点のリスナーと X 方向に伸びるエミッタの両方が入る画角。</summary>
        private static void CreateCamera()
        {
            var cameraObject = new GameObject("[Debug] Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 22f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(15f, 40f, 0f), Quaternion.Euler(90f, 0f, 0f));
            cameraObject.tag = "MainCamera";

            // AudioListener はカメラに付けない。カメラ位置が距離の基準になると
            // 真上 40m から測ることになり、近い音と遠い音の差が潰れてしまう。
        }

        private static Transform CreateListener()
        {
            var listener = new GameObject("[Debug] Listener");
            listener.AddComponent<AudioListener>();
            AddMarker(listener.transform, 1.5f);
            return listener.transform;
        }

        private static Transform CreateEmitter()
        {
            var emitter = new GameObject("[Debug] SeEmitter");
            AddMarker(emitter.transform, 0.8f);
            return emitter.transform;
        }

        /// <summary>位置が目で分かるようにする球。当たり判定は要らないので外す。</summary>
        private static void AddMarker(Transform parent, float diameter)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker";
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = Vector3.one * diameter;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static void CreateDebugView(
            Transform listener, Transform emitter,
            AudioClip bgmA, AudioClip bgmB, AudioClip click, AudioClip hit, AudioClip footstep)
        {
            var holder = new GameObject("[Debug] AudioDebugView");
            var view = holder.AddComponent<AudioDebugView>();

            // private [SerializeField] なので SerializedObject 経由で差し込む。
            // キー割り当てには触らない。触ると Editor 側の asmdef に Input System の参照が要る。
            var serialized = new SerializedObject(view);
            serialized.FindProperty("bgmClipA").objectReferenceValue = bgmA;
            serialized.FindProperty("bgmClipB").objectReferenceValue = bgmB;
            serialized.FindProperty("se2dClip").objectReferenceValue = click;
            serialized.FindProperty("se3dClip").objectReferenceValue = hit;
            serialized.FindProperty("seFootstepClip").objectReferenceValue = footstep;
            serialized.FindProperty("listenerAnchor").objectReferenceValue = listener;
            serialized.FindProperty("emitter").objectReferenceValue = emitter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Build Settings に足す。ただし無効で登録する。
        /// 確認用のシーンを製品のビルドに含めたくないが、一覧からは見えるようにしておきたい。
        /// Project ウィンドウから開く分には Build Settings と関係ないので手動確認には支障がない。
        /// </summary>
        private static void AddToBuildSettings(string scenePath)
        {
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == scenePath) return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(scenePath, false),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
