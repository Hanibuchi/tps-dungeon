using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud.Editor
{
    /// <summary>
    /// パーティーの操作台（Party.prefab）に、全滅したときの「力尽きた」（<see cref="GameOverView"/>）を組み込む。
    /// HUD とメニューより手前、暗転（ScreenFade）より奥に描く。
    /// </summary>
    public static class GameOverSetup
    {
        private const string PartyPrefabPath = "Assets/_Project/Prefabs/Character/Party.prefab";
        private const string UxmlPath = "Assets/_Project/UI/Hud/GameOver.uxml";
        private const string PanelSettingsPath = "Assets/_Project/Settings/UI/GamePanelSettings.asset";
        private const string ChildName = "Game Over";

        // HUD（-10）・インベントリ（5）・ポーズ（10）より手前。
        private const float SortingOrder = 20f;

        [MenuItem("Tools/TPS Dungeon/組み込み/全滅の画面", false, 312)]
        public static void Setup()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (uxml == null || panelSettings == null)
            {
                Debug.LogError($"全滅の画面の素材が見つからない: {UxmlPath} / {PanelSettingsPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            try
            {
                Transform child = root.transform.Find(ChildName);
                if (child == null)
                {
                    child = new GameObject(ChildName).transform;
                    child.SetParent(root.transform, false);
                }

                var document = child.GetComponent<UIDocument>();
                if (document == null) document = child.gameObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.sortingOrder = SortingOrder;

                if (child.GetComponent<GameOverView>() == null) child.gameObject.AddComponent<GameOverView>();

                PrefabUtility.SaveAsPrefabAsset(root, PartyPrefabPath);
                Debug.Log($"全滅の画面を組み込んだ: {PartyPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
