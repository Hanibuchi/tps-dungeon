using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Interaction.Editor
{
    /// <summary>
    /// プレイヤーのプレハブにインタラクトの仕組み（PlayerInteractor と照準・プロンプトの HUD）を組み込む。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// </summary>
    public static class PlayerInteractionSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string HudUxmlPath = "Assets/_Project/UI/Interaction/InteractionHud.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string HudName = "Interaction HUD";

        // 設定画面（GameAudio の Settings Panel、sortingOrder 0）より下に描く。
        private const float HudSortingOrder = -10f;

        [MenuItem("Tools/TPS Dungeon/Player/インタラクトを組み込む")]
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
                var interactor = root.GetComponent<PlayerInteractor>();
                if (interactor == null) interactor = root.AddComponent<PlayerInteractor>();

                var serializedInteractor = new SerializedObject(interactor);
                serializedInteractor.FindProperty("playerInput").objectReferenceValue = root.GetComponent<PlayerInput>();
                serializedInteractor.ApplyModifiedPropertiesWithoutUndo();

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

                var view = hud.GetComponent<InteractionPromptView>();
                if (view == null) view = hud.gameObject.AddComponent<InteractionPromptView>();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("interactor").objectReferenceValue = interactor;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"インタラクトを組み込んだ: {PlayerPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
