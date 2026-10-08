using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud.Editor
{
    /// <summary>
    /// パーティーの操作台のプレハブに常時表示の HUD（HP・ホットバー・マップ・ダメージの数字）と、大きな地図の開け閉め（PlayerMapToggle）を組み込む。
    /// HP・ホットバーなどキャラの分は、実行中にパーティーの先頭を写す（GameHudView が PartyRoster から結ぶ）ので、ここでは結ばない。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// 操作台は「Tools/TPS Dungeon/Party/パーティーを組み込む」が作る。
    /// </summary>
    public static class PlayerHudSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
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
                var mapToggle = root.GetComponent<PlayerMapToggle>();
                if (mapToggle == null) mapToggle = root.AddComponent<PlayerMapToggle>();

                var playerInput = root.GetComponent<PlayerInput>();
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
                serializedView.FindProperty("health").objectReferenceValue = null;
                serializedView.FindProperty("progression").objectReferenceValue = null;
                serializedView.FindProperty("hotbar").objectReferenceValue = null;
                serializedView.FindProperty("inventory").objectReferenceValue = null;
                serializedView.FindProperty("mapToggle").objectReferenceValue = mapToggle;
                serializedView.FindProperty("player").objectReferenceValue = null;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                var damageNumbers = hud.GetComponent<DamageNumberView>();
                if (damageNumbers == null) damageNumbers = hud.gameObject.AddComponent<DamageNumberView>();
                SetReference(damageNumbers, "attacker", null);
                SetReference(damageNumbers, "ranged", null);
                SetReference(view, "attacker", null);
                SetReference(view, "ranged", null);

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
