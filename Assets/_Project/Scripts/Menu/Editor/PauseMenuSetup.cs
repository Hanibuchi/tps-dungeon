using TpsDungeon.Menu.UI;
using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに、Esc で開くポーズメニュー（設定画面つき）と、操作の設定を当てる
    /// PlayerControlSettings を組み込む。何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class PauseMenuSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string MenuUxmlPath = "Assets/_Project/UI/Menu/PauseMenu.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string PreviewClipPath = "Assets/_Project/Audio/Placeholder/SE_Click.wav";
        private const string MenuName = "Pause Menu";

        // HUD（-10）や照準の案内より上に描く。
        private const float MenuSortingOrder = 10f;

        [MenuItem("Tools/TPS Dungeon/Player/ポーズメニューを組み込む")]
        public static void Setup()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuUxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (uxml == null || panelSettings == null)
            {
                Debug.LogError($"ポーズメニューの素材が見つからない: {MenuUxmlPath} / {PanelSettingsPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var playerInput = root.GetComponent<PlayerInput>();
                if (playerInput == null)
                {
                    Debug.LogError($"{PlayerPrefabPath} に PlayerInput が無い");
                    return;
                }

                var controls = root.GetComponent<PlayerControlSettings>();
                if (controls == null) controls = root.AddComponent<PlayerControlSettings>();
                SetReference(controls, "playerInput", playerInput);

                var holder = root.transform.Find(MenuName);
                if (holder == null)
                {
                    holder = new GameObject(MenuName).transform;
                    holder.SetParent(root.transform, false);
                }

                var document = holder.GetComponent<UIDocument>();
                if (document == null) document = holder.gameObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.sortingOrder = MenuSortingOrder;

                var menu = holder.GetComponent<PauseMenu>();
                if (menu == null) menu = holder.gameObject.AddComponent<PauseMenu>();

                var serialized = new SerializedObject(menu);
                serialized.FindProperty("playerInput").objectReferenceValue = playerInput;
                serialized.FindProperty("controls").objectReferenceValue = controls;
                serialized.FindProperty("mapToggle").objectReferenceValue = root.GetComponent<PlayerMapToggle>();
                serialized.FindProperty("previewClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewClipPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"ポーズメニューを組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
