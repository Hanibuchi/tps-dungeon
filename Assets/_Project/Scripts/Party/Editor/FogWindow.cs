using System.Collections.Generic;
using TpsDungeon.Map.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party.Editor
{
    /// <summary>
    /// 霧を調節する窓（Tools/TPS Dungeon/霧の調節）。値は <see cref="FogSettings"/> のアセットに持つ。
    /// - 値を変えると、開いているシーンの霧にすぐ入れて見せる（シーンは保存しない）。Play 中でも見ながら調節でき、値はアセットに残る。
    /// - 「地上と全部の階に書き込む」で、地上と道順（DungeonRoute）に載っている階のシーン全部の霧に入れて保存する（Play 中は押せない）。
    ///   未保存の変更があるシーンは、霧だけ入れて保存はしない（ユーザの変更を巻き込まない）。
    /// </summary>
    public sealed class FogWindow : EditorWindow
    {
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        private FogSettings settings;
        private string message;

        [MenuItem("Tools/TPS Dungeon/霧の調節")]
        public static void Open() => GetWindow<FogWindow>("霧の調節");

        private void OnEnable()
        {
            settings = FogSettings.LoadOrCreate();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            if (settings == null) return;
            settings.Apply();
            Repaint();
        }

        private void OnGUI()
        {
            if (settings == null) settings = FogSettings.LoadOrCreate();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("設定", settings, typeof(FogSettings), false);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            Color color = EditorGUILayout.ColorField(new GUIContent("色", "霧の色。空の色に寄せると遠くが空に溶ける。"), settings.color);
            var mode = (FogMode)EditorGUILayout.EnumPopup(new GUIContent("かかり方", "Linear は距離で、Exponential・ExponentialSquared は濃さで決まる。"), settings.mode);
            float start = settings.startDistance;
            float end = settings.endDistance;
            float density = settings.density;
            if (mode == FogMode.Linear)
            {
                start = EditorGUILayout.Slider(new GUIContent("始まる距離 (m)", "ここから霧がかかり始める。小さいほど近くから霞む。"), start, 0f, 1000f);
                end = EditorGUILayout.Slider(new GUIContent("真っ白になる距離 (m)", "ここで霧の色一色になる。小さいほど濃い。"), end, 1f, 1500f);
            }
            else
            {
                density = EditorGUILayout.Slider(new GUIContent("濃さ", "大きいほど濃い。"), density, 0f, 0.05f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "霧の調節");
                settings.color = color;
                settings.mode = mode;
                settings.startDistance = start;
                settings.endDistance = Mathf.Max(start + 1f, end);
                settings.density = density;
                EditorUtility.SetDirty(settings);
                settings.Apply();
                message = null;
            }

            EditorGUILayout.HelpBox("値を変えると、開いているシーンの霧にすぐ入ります（シーンは保存しません）。Play 中に変えた値も設定に残ります。", MessageType.None);
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("開いているシーンの霧を読み込む", "いまのシーンの霧の値を設定に写す。")))
            {
                Undo.RecordObject(settings, "霧を読み込む");
                settings.ReadFromScene();
                EditorUtility.SetDirty(settings);
                message = $"{SceneManager.GetActiveScene().name} の霧を読み込んだ。";
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(new GUIContent("地上と全部の階に書き込む", "地上と道順に載っている階のシーン全部の霧にこの値を入れて保存する。")))
                {
                    AssetDatabase.SaveAssetIfDirty(settings);
                    message = WriteToAllScenes(settings);
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("全部のシーンへの書き込みは Play を止めてから。", MessageType.Info);

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        /// <summary>地上と道順の階のシーン全部の霧に settings を入れて保存し、何をしたかを返す。</summary>
        private static string WriteToAllScenes(FogSettings settings)
        {
            var paths = new List<string> { SurfaceSceneBuilder.ScenePath };
            var route = AssetDatabase.LoadAssetAtPath<DungeonRoute>(RoutePath);
            if (route != null)
            {
                foreach (string name in route.SceneNames()) paths.Add($"Assets/_Project/Scenes/{name}.unity");
            }

            int saved = 0;
            var skipped = new List<string>();
            Scene previous = SceneManager.GetActiveScene();
            foreach (string path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;

                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.IsValid() && scene.isLoaded;
                bool wasDirty = wasOpen && scene.isDirty;
                if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                // 霧（RenderSettings）はアクティブなシーンに入るので、入れる間だけアクティブにする。
                SceneManager.SetActiveScene(scene);
                try
                {
                    settings.Apply();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (wasDirty) skipped.Add(scene.name);
                    else if (EditorSceneManager.SaveScene(scene)) saved++;
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                }
            }

            string result = $"{saved} 個のシーンの霧を書き換えて保存した。";
            if (skipped.Count > 0) result += $"\n{string.Join("・", skipped)} は未保存の変更があったので、霧だけ入れて保存していない。";
            return result;
        }
    }
}
