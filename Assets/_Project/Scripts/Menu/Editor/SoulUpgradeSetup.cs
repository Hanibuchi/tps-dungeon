using TpsDungeon.Menu.UI;
using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.Editor
{
    /// <summary>
    /// パーティーの操作台のプレハブ（Party.prefab）に、魂の強化の画面（死後の場所の燭台で開く）と、
    /// 転生で持ち越す武器を選ぶ画面を組み込む。何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class SoulUpgradeSetup
    {
        private const string PartyPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string UpgradeUxmlPath = "Assets/_Project/UI/Menu/SoulUpgrade.uxml";
        private const string CarryOverUxmlPath = "Assets/_Project/UI/Menu/CarryOver.uxml";
        private const string UpgradeName = "Soul Upgrade Screen";
        private const string CarryOverName = "Carry Over Screen";

        // インベントリ（5）より上、ポーズ（10）より下。持ち越しは転生の白い幕の代わりに全面を覆うので一番上。
        private const float UpgradeSortingOrder = 8f;
        private const float CarryOverSortingOrder = 40f;

        [MenuItem("Tools/TPS Dungeon/Player/魂の強化の画面を組み込む")]
        public static void Setup()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var upgradeUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UpgradeUxmlPath);
            var carryUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CarryOverUxmlPath);
            if (panelSettings == null || upgradeUxml == null || carryUxml == null)
            {
                Debug.LogError($"魂の強化の画面の素材が見つからない: {PanelSettingsPath} / {UpgradeUxmlPath} / {CarryOverUxmlPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                var playerInput = root.GetComponent<PlayerInput>();
                if (playerInput == null)
                {
                    Debug.LogError($"{PartyPrefabPath} に PlayerInput が無い");
                    return;
                }

                Place<SoulUpgradeScreen>(root, UpgradeName, panelSettings, upgradeUxml, UpgradeSortingOrder, playerInput);
                Place<CarryOverScreen>(root, CarryOverName, panelSettings, carryUxml, CarryOverSortingOrder, playerInput);

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                Debug.Log($"魂の強化の画面を組み込んだ: {PartyPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Place<T>(GameObject root, string name, PanelSettings panelSettings, VisualTreeAsset uxml, float sortingOrder,
            PlayerInput playerInput) where T : MonoBehaviour
        {
            var holder = root.transform.Find(name);
            if (holder == null)
            {
                holder = new GameObject(name).transform;
                holder.SetParent(root.transform, false);
            }

            var document = holder.GetComponent<UIDocument>();
            if (document == null) document = holder.gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.sortingOrder = sortingOrder;

            var screen = holder.GetComponent<T>();
            if (screen == null) screen = holder.gameObject.AddComponent<T>();

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("playerInput").objectReferenceValue = playerInput;
            serialized.FindProperty("controls").objectReferenceValue = root.GetComponent<PlayerControlSettings>();
            serialized.FindProperty("mapToggle").objectReferenceValue = root.GetComponent<PlayerMapToggle>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
