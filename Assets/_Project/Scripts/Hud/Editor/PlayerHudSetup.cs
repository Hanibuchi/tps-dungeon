using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに常時表示の HUD（HP・ホットバー・マップ）と、その出どころの
    /// PlayerHealth / PlayerHotbar / PlayerMapToggle を組み込む。何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class PlayerHudSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string HudUxmlPath = "Assets/_Project/UI/Hud/GameHud.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string HudName = "Game HUD";

        // 照準の HUD と同じく、設定画面（sortingOrder 0）より下に描く。
        private const float HudSortingOrder = -10f;

        [MenuItem("Tools/TPS Dungeon/Player/HUD を組み込む")]
        public static void Setup()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (uxml == null || panelSettings == null)
            {
                Debug.LogError($"HUD の素材が見つからない: {HudUxmlPath} / {PanelSettingsPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var health = root.GetComponent<PlayerHealth>();
                if (health == null) health = root.AddComponent<PlayerHealth>();

                var hotbar = root.GetComponent<PlayerHotbar>();
                if (hotbar == null) hotbar = root.AddComponent<PlayerHotbar>();

                var mapToggle = root.GetComponent<PlayerMapToggle>();
                if (mapToggle == null) mapToggle = root.AddComponent<PlayerMapToggle>();

                var playerInput = root.GetComponent<PlayerInput>();
                SetReference(hotbar, "playerInput", playerInput);
                SetReference(mapToggle, "playerInput", playerInput);

                var hud = root.transform.Find(HudName);
                if (hud == null)
                {
                    hud = new GameObject(HudName).transform;
                    hud.SetParent(root.transform, false);
                }

                var document = hud.GetComponent<UIDocument>();
                if (document == null) document = hud.gameObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.sortingOrder = HudSortingOrder;

                var view = hud.GetComponent<GameHudView>();
                if (view == null) view = hud.gameObject.AddComponent<GameHudView>();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("health").objectReferenceValue = health;
                serializedView.FindProperty("hotbar").objectReferenceValue = hotbar;
                serializedView.FindProperty("mapToggle").objectReferenceValue = mapToggle;
                serializedView.FindProperty("player").objectReferenceValue = root.transform;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"HUD を組み込んだ: {PlayerPrefabPath}");
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
