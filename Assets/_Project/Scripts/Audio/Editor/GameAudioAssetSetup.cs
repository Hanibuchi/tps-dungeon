using System.IO;
using System.Linq;
using TpsDungeon.Audio.Authoring;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Audio.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UIElements;

namespace TpsDungeon.Audio.Editor
{
    /// <summary>
    /// GameAudioConfig アセットと、自動起動用の GameAudio プレハブを作り直すエディタ専用ツール。
    /// ミキサーのグループやスナップショットへの参照を手で繋ぐと付け間違えるので、
    /// GameAudioMixerSetup が決めた名前を使ってここで機械的に紐付ける。
    ///
    /// 設定画面もこのプレハブの子として持たせている。シーンをまたいで生き残るので、
    /// どのシーンを再生しても Esc で音量設定を出せる。
    /// </summary>
    public static class GameAudioAssetSetup
    {
        public const string ConfigPath = "Assets/_Project/Settings/Audio/GameAudioConfig.asset";
        public const string PrefabPath = "Assets/_Project/Resources/Audio/GameAudio.prefab";
        public const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        public const string ThemePath = "Assets/_Project/Settings/UI/GameRuntimeTheme.tss";
        public const string PanelUxmlPath = "Assets/_Project/UI/Audio/AudioSettingsPanel.uxml";
        public const string PreviewClipPath = "Assets/_Project/Audio/Placeholder/SE_Click.wav";

        [MenuItem("Tools/TPS Dungeon/Audio/Generate Game Audio Config And Prefab")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>設定アセットとプレハブを作り直して、何をしたかのログを返す。</summary>
        public static string Generate()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(GameAudioMixerSetup.MixerPath);
            if (mixer == null)
            {
                return "先に Generate Game Audio Mixer を実行すること。ミキサーが無い: " + GameAudioMixerSetup.MixerPath;
            }

            GameAudioConfig config = LoadOrCreateConfig();
            WireConfig(config, mixer);

            PanelSettings panelSettings = LoadOrCreatePanelSettings();
            GameObject prefab = CreateOrUpdatePrefab(config, panelSettings);

            AssetDatabase.SaveAssets();
            return "オーディオの設定一式を用意した:\n  " + ConfigPath + "\n  " + PanelSettingsPath + "\n  " + PrefabPath
                   + "\n  prefab=" + (prefab != null ? prefab.name : "作成失敗");
        }

        private static GameAudioConfig LoadOrCreateConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));

            var config = AssetDatabase.LoadAssetAtPath<GameAudioConfig>(ConfigPath);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<GameAudioConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        /// <summary>ミキサーのグループとスナップショットを名前で引いて設定に差す。</summary>
        private static void WireConfig(GameAudioConfig config, AudioMixer mixer)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mixer").objectReferenceValue = mixer;
            serialized.FindProperty("bgmGroup").objectReferenceValue = FindGroup(mixer, GameAudioMixerSetup.BgmGroupName);
            serialized.FindProperty("seGroup").objectReferenceValue = FindGroup(mixer, GameAudioMixerSetup.SeGroupName);

            serialized.FindProperty("masterVolumeParameter").stringValue = GameAudioMixerSetup.MasterVolumeParam;
            serialized.FindProperty("bgmVolumeParameter").stringValue = GameAudioMixerSetup.BgmVolumeParam;
            serialized.FindProperty("seVolumeParameter").stringValue = GameAudioMixerSetup.SeVolumeParam;

            serialized.FindProperty("defaultSnapshot").objectReferenceValue = mixer.FindSnapshot(GameAudioMixerSetup.DefaultSnapshotName);
            serialized.FindProperty("dungeonSnapshot").objectReferenceValue = mixer.FindSnapshot(GameAudioMixerSetup.DungeonSnapshotName);
            serialized.FindProperty("pausedSnapshot").objectReferenceValue = mixer.FindSnapshot(GameAudioMixerSetup.PausedSnapshotName);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        /// <summary>FindMatchingGroups はパスの部分一致なので、名前が完全一致するものだけ拾う。</summary>
        private static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            return mixer.FindMatchingGroups(groupName).FirstOrDefault(group => group.name == groupName);
        }

        /// <summary>
        /// UI Toolkit の土台。テーマを差し忘れるとコントロールの見た目が当たらず画面が真っ白になる。
        /// </summary>
        private static PanelSettings LoadOrCreatePanelSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath));

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) Debug.LogWarning("ランタイムテーマが見つからない: " + ThemePath);

            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);

            return panelSettings;
        }

        private static GameObject CreateOrUpdatePrefab(GameAudioConfig config, PanelSettings panelSettings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var root = new GameObject("GameAudio");
            try
            {
                var audio = root.AddComponent<GameAudio>();

                var serialized = new SerializedObject(audio);
                serialized.FindProperty("config").objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                AddSettingsPanel(root, panelSettings);

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>設定画面を GameAudio の子として組む。既定では閉じた状態。</summary>
        private static void AddSettingsPanel(GameObject root, PanelSettings panelSettings)
        {
            var holder = new GameObject("Settings Panel");
            holder.transform.SetParent(root.transform, false);

            var document = holder.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelUxmlPath);

            var panel = holder.AddComponent<AudioSettingsPanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("previewClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewClipPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
