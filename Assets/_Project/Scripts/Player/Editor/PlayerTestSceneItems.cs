using TpsDungeon.Items;
using TpsDungeon.Items.Editor;
using TpsDungeon.Map.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Player.Editor
{
    /// <summary>
    /// 確認用シーン（Player_Debug）の入口の部屋に、拾う・インベントリを試すための仮アイテムを置く。
    /// シーンのほかの部分には触らず、[Items] の下だけを作り直す。何度実行しても同じ結果になる。
    /// </summary>
    public static class PlayerTestSceneItems
    {
        private const string RootName = "[Items]";

        /// <summary>
        /// 入口の部屋の FeatureCell 中心からの置き場所。中心の階段マーカー（1.4 角）と、
        /// その +X 側 1.5m のスポーン地点を避け、セル（5m）の内側に収める。ホットバー 4 枠を越えてバッグに入るのを試せる数にする。
        /// </summary>
        private static readonly Vector3[] Offsets =
        {
            new Vector3(-1.6f, 0f, -1.6f),
            new Vector3(-1.6f, 0f, 0f),
            new Vector3(-1.6f, 0f, 1.6f),
            new Vector3(0f, 0f, 1.7f),
            new Vector3(0f, 0f, -1.7f),
            new Vector3(1.6f, 0f, 1.6f),
        };

        [MenuItem("Tools/TPS Dungeon/Player/確認用シーンに仮アイテムを置く")]
        public static void PlaceFromMenu()
        {
            Debug.Log(Place());
        }

        public static string Place()
        {
            var items = PlaceholderItemAssetGenerator.LoadAll();
            if (items.Count == 0)
            {
                return "先に Tools/TPS Dungeon/プレースホルダのアイテムを生成 を実行すること。";
            }

            var config = AssetDatabase.LoadAssetAtPath<FloorConfig>(PlayerTestSceneBuilder.ConfigPath);
            if (config == null) return "フロアの設定が無い: " + PlayerTestSceneBuilder.ConfigPath;
            if (!PlayerTestSceneBuilder.TryFindEntrance(config, out Vector3 center, out string note))
            {
                return "置き場所を決められなかった。" + note;
            }

            Scene scene = SceneManager.GetSceneByPath(PlayerTestSceneBuilder.ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                // エディタでは開いているシーンを巻き込まないよう追加で開く。batchmode は無題シーンのままだと追加で開けないので単独で。
                var mode = Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive;
                scene = EditorSceneManager.OpenScene(PlayerTestSceneBuilder.ScenePath, mode);
            }
            else if (scene.isDirty)
            {
                return "Player_Debug に保存していない変更があるので置かなかった。保存してからもう一度実行すること。";
            }

            try
            {
                foreach (GameObject existing in scene.GetRootGameObjects())
                {
                    if (existing.name == RootName) Object.DestroyImmediate(existing);
                }

                var root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);

                for (int i = 0; i < Offsets.Length; i++)
                {
                    ItemDefinition item = items[i % items.Count];
                    var pickup = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldPrefab.gameObject, scene);
                    pickup.transform.SetParent(root.transform, false);
                    pickup.transform.SetPositionAndRotation(center + Offsets[i], Quaternion.Euler(0f, 37f * i, 0f));
                }

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere && !Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
            }

            return $"確認用シーンに仮アイテムを {Offsets.Length} 個置いた: {PlayerTestSceneBuilder.ScenePath}（{note}）";
        }
    }
}
