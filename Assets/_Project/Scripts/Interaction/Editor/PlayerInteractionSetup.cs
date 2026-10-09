using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Interaction.Editor
{
    /// <summary>
    /// インタラクトの仕組みを組み込む。PlayerInteractor はキャラのプレハブに（先頭のキャラだけで有効になる）、
    /// 照準・プロンプトの HUD はパーティーの操作台のプレハブに置く（今有効な PlayerInteractor を写す）。
    /// 何度実行しても同じ結果になる（既にあれば設定だけ入れ直す）。
    /// 操作台は「Tools/TPS Dungeon/Party/パーティーを組み込む」が作る。
    /// </summary>
    public static class PlayerInteractionSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Character/Character Variant.prefab";
        private const string PartyPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
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

                // PlayerInput はパーティーの操作台が持つので、実行中にシーンから探させる。
                var serializedInteractor = new SerializedObject(interactor);
                serializedInteractor.FindProperty("playerInput").objectReferenceValue = null;
                serializedInteractor.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath) == null)
            {
                Debug.LogError($"パーティーの操作台が無いので HUD を組み込めない: {PartyPrefabPath}");
                return;
            }

            var party = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                var hud = party.transform.Find(HudName);
                if (hud == null)
                {
                    hud = new GameObject(HudName).transform;
                    hud.SetParent(party.transform, false);
                }

                var document = hud.GetComponent<UIDocument>();
                if (document == null) document = hud.gameObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.sortingOrder = HudSortingOrder;

                var view = hud.GetComponent<InteractionPromptView>();
                if (view == null) view = hud.gameObject.AddComponent<InteractionPromptView>();

                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("interactor").objectReferenceValue = null;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(party, PartyPrefabPath);
                Debug.Log($"インタラクトを組み込んだ: {PlayerPrefabPath} / {PartyPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(party);
            }
        }
    }
}
