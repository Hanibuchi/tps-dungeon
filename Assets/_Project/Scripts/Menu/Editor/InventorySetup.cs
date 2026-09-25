using TpsDungeon.Items;
using TpsDungeon.Menu.UI;
using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.Editor
{
    /// <summary>
    /// プレイヤーのプレハブに、持ち物（PlayerInventory）と Tab で開くインベントリ画面を組み込む。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class InventorySetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string ScreenUxmlPath = "Assets/_Project/UI/Menu/Inventory.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string ScreenName = "Inventory Screen";

        // HUD（-10）より上、ポーズメニュー（10）より下に描く。
        private const float ScreenSortingOrder = 5f;

        [MenuItem("Tools/TPS Dungeon/Player/インベントリを組み込む")]
        public static void Setup()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ScreenUxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (uxml == null || panelSettings == null)
            {
                Debug.LogError($"インベントリ画面の素材が見つからない: {ScreenUxmlPath} / {PanelSettingsPath}");
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

                var inventory = root.GetComponent<PlayerInventory>();
                if (inventory == null) inventory = root.AddComponent<PlayerInventory>();

                var holder = root.transform.Find(ScreenName);
                if (holder == null)
                {
                    holder = new GameObject(ScreenName).transform;
                    holder.SetParent(root.transform, false);
                }

                var document = holder.GetComponent<UIDocument>();
                if (document == null) document = holder.gameObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.sortingOrder = ScreenSortingOrder;

                var screen = holder.GetComponent<InventoryScreen>();
                if (screen == null) screen = holder.gameObject.AddComponent<InventoryScreen>();

                var serialized = new SerializedObject(screen);
                serialized.FindProperty("playerInput").objectReferenceValue = playerInput;
                serialized.FindProperty("inventory").objectReferenceValue = inventory;
                serialized.FindProperty("hotbar").objectReferenceValue = root.GetComponent<PlayerHotbar>();
                serialized.FindProperty("controls").objectReferenceValue = root.GetComponent<PlayerControlSettings>();
                serialized.FindProperty("mapToggle").objectReferenceValue = root.GetComponent<PlayerMapToggle>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"インベントリを組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
