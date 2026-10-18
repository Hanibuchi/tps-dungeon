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
    /// - 塔のまわりのもや … 色と濃さは素材なので全部のシーンにすぐ効く。量・大きさ・高さは、開いているシーン（地上か階）のもやを置き直して見せる。
    /// - 遠くの霧 … 開いているシーンの霧にすぐ入れて見せる。
    /// どちらも開いているシーンは保存しない。Play 中でも見ながら調節でき、値はアセットに残る。
    /// 「地上と全部の階に書き込む」で、地上と道順（DungeonRoute）に載っている階のシーン全部のもやと霧を置き直して保存する（Play 中は押せない）。
    /// 未保存の変更があるシーンは、置き直すだけで保存はしない（ユーザの変更を巻き込まない）。
    /// </summary>
    public sealed class FogWindow : EditorWindow
    {
        private const string RoutePath = "Assets/_Project/Settings/Map/DungeonRoute.asset";

        private FogSettings settings;
        private string message;
        private Vector2 scroll;

        [MenuItem("Tools/TPS Dungeon/霧の調節")]
        public static void Open() => GetWindow<FogWindow>("霧の調節");

        private void OnEnable()
        {
            settings = FogSettings.LoadOrCreate();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.delayCall -= RebuildActiveMist;
        }

        private void OnUndoRedo()
        {
            if (settings == null) return;
            settings.Apply();
            MistBuilder.ApplyColor(settings);
            QueueMistRebuild();
            Repaint();
        }

        private void OnGUI()
        {
            if (settings == null) settings = FogSettings.LoadOrCreate();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("設定", settings, typeof(FogSettings), false);

            DrawMist();
            DrawDistanceFog();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("値を変えると、開いているシーンにすぐ入ります（シーンは保存しません）。Play 中に変えた値も設定に残ります。", MessageType.None);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(new GUIContent("地上と全部の階に書き込む", "地上と道順に載っている階のシーン全部のもやと霧を、この値で置き直して保存する。")))
                {
                    AssetDatabase.SaveAssetIfDirty(settings);
                    message = WriteToAllScenes(settings);
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("全部のシーンへの書き込みは Play を止めてから。", MessageType.Info);

            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMist()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("塔のまわりのもや", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Color color = EditorGUILayout.ColorField(new GUIContent("色"), settings.mistColor);
            float opacity = EditorGUILayout.Slider(new GUIContent("濃さ", "もや 1 枚の不透明さ。"), settings.mistOpacity, 0f, 1f);
            bool colorChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            float amount = EditorGUILayout.Slider(new GUIContent("量", "もやの枚数の倍率。0 で無し。"), settings.mistAmount, 0f, 3f);
            float size = EditorGUILayout.Slider(new GUIContent("大きさ", "もや 1 枚の大きさの倍率。"), settings.mistSize, 0.3f, 3f);
            float height = EditorGUILayout.Slider(new GUIContent("地上で塔のどこまで (m)", "塔の側面のもやをどの高さまでまとわせるか。根元のもやは高さに関係なく出す。"), settings.mistHeight, 0f, 200f);
            bool shapeChanged = EditorGUI.EndChangeCheck();

            if (!colorChanged && !shapeChanged) return;
            Undo.RecordObject(settings, "もやの調節");
            settings.mistColor = color;
            settings.mistOpacity = opacity;
            settings.mistAmount = amount;
            settings.mistSize = size;
            settings.mistHeight = height;
            EditorUtility.SetDirty(settings);
            if (colorChanged) MistBuilder.ApplyColor(settings);
            if (shapeChanged) QueueMistRebuild();
            message = null;
        }

        private void DrawDistanceFog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("遠くの霧（景色全体）", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(new GUIContent("かける", "景色全体を遠いほど霞ませる。"), settings.distanceFog);
            Color color = settings.color;
            FogMode mode = settings.mode;
            float start = settings.startDistance;
            float end = settings.endDistance;
            float density = settings.density;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                color = EditorGUILayout.ColorField(new GUIContent("色", "空の色に寄せると遠くが空に溶ける。"), color);
                mode = (FogMode)EditorGUILayout.EnumPopup(new GUIContent("かかり方", "Linear は距離で、Exponential・ExponentialSquared は濃さで決まる。"), mode);
                if (mode == FogMode.Linear)
                {
                    start = EditorGUILayout.Slider(new GUIContent("始まる距離 (m)", "ここから霧がかかり始める。小さいほど近くから霞む。"), start, 0f, 1000f);
                    end = EditorGUILayout.Slider(new GUIContent("真っ白になる距離 (m)", "ここで霧の色一色になる。小さいほど濃い。"), end, 1f, 1500f);
                }
                else
                {
                    density = EditorGUILayout.Slider(new GUIContent("濃さ", "大きいほど濃い。"), density, 0f, 0.05f);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "霧の調節");
                settings.distanceFog = enabled;
                settings.color = color;
                settings.mode = mode;
                settings.startDistance = start;
                settings.endDistance = Mathf.Max(start + 1f, end);
                settings.density = density;
                EditorUtility.SetDirty(settings);
                settings.Apply();
                message = null;
            }

            if (GUILayout.Button(new GUIContent("開いているシーンの霧を読み込む", "いまのシーンの遠くの霧の値を設定に写す。")))
            {
                Undo.RecordObject(settings, "霧を読み込む");
                settings.ReadFromScene();
                EditorUtility.SetDirty(settings);
                message = $"{SceneManager.GetActiveScene().name} の霧を読み込んだ。";
            }
        }

        /// <summary>スライダーを動かしている間に何度も置き直さないよう、落ち着いてから 1 回だけ置き直す。</summary>
        private void QueueMistRebuild()
        {
            EditorApplication.delayCall -= RebuildActiveMist;
            EditorApplication.delayCall += RebuildActiveMist;
        }

        private void RebuildActiveMist()
        {
            if (settings == null) return;
            Scene scene = SceneManager.GetActiveScene();
            message = BuildMist(scene, settings) ?? $"{scene.name} は地上でも階でもないので、もやは置き直していない。";
            Repaint();
        }

        /// <summary>scene が地上か階なら、もやを置き直して何をしたかを返す。どちらでもなければ null。scene はアクティブにしておくこと。</summary>
        private static string BuildMist(Scene scene, FogSettings settings)
        {
            if (scene.path == SurfaceSceneBuilder.ScenePath) return MistBuilder.BuildSurface(scene, settings);

            FloorConfig config = SurfaceScenery.FindFloorConfig(scene);
            if (config == null) return null;
            Vector3 center = SurfaceScenery.OutlookCenter(config);
            return MistBuilder.BuildFloor(scene, new Vector3(center.x, 0f, center.z), config.elevation, center.x * 2f, settings);
        }

        /// <summary>地上と道順の階のシーン全部のもやと遠くの霧を settings で置き直して保存し、何をしたかを返す。</summary>
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

                // 霧（RenderSettings）と新しく作る物はアクティブなシーンに入るので、置く間だけアクティブにする。
                SceneManager.SetActiveScene(scene);
                try
                {
                    settings.Apply();
                    BuildMist(scene, settings);
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

            string result = $"{saved} 個のシーンのもやと霧を置き直して保存した。";
            if (skipped.Count > 0) result += $"\n{string.Join("・", skipped)} は未保存の変更があったので、置き直しただけで保存していない。";
            return result;
        }
    }
}
