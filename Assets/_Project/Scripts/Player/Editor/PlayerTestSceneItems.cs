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
    /// 確認用シーン（Player_Debug）の入口の部屋に、拾う・インベントリを試すための仮アイテムと仮の武器を置き、
    /// 扉をくぐった先に試し斬り用の敵を置く。
    /// シーンのほかの部分には触らず、[Items] と [Enemies] の下だけを作り直す。何度実行しても同じ結果になる。
    /// </summary>
    public static class PlayerTestSceneItems
    {
        private const string RootName = "[Items]";
        private const string EnemiesRootName = "[Enemies]";

        private static readonly string[] EnemyPrefabs =
        {
            "Assets/_Project/Prefabs/Enemies/Big_Orc.prefab",
            "Assets/_Project/Prefabs/Enemies/Blob_GreenBlob.prefab",
        };

        /// <summary>扉の先のセルの中心から見た敵の置き場所（入口へ向かう向きを +Z とする）。</summary>
        private static readonly Vector3[] EnemyOffsets =
        {
            new Vector3(-1.1f, 0f, -0.5f),
            new Vector3(1.1f, 0f, -0.5f),
        };

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
        };

        /// <summary>仮の武器の置き場所。スポーン地点（+X 側）の近くに寄せて、すぐ拾えるようにする。</summary>
        private static readonly Vector3[] WeaponOffsets =
        {
            new Vector3(0f, 0f, -1.7f),
            new Vector3(1.6f, 0f, 1.6f),
            new Vector3(1.6f, 0f, -1.6f),
        };

        [MenuItem("Tools/TPS Dungeon/Player/確認用シーンに仮アイテムを置く")]
        public static void PlaceFromMenu()
        {
            Debug.Log(Place());
        }

        public static string Place()
        {
            string enemyNote = string.Empty;
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
                    if (existing.name == RootName || existing.name == EnemiesRootName) Object.DestroyImmediate(existing);
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

                var weapons = PlaceholderWeaponAssetGenerator.LoadAll();
                for (int i = 0; i < weapons.Count && i < WeaponOffsets.Length; i++)
                {
                    if (weapons[i].WorldPrefab == null) continue;
                    var pickup = (GameObject)PrefabUtility.InstantiatePrefab(weapons[i].WorldPrefab.gameObject, scene);
                    pickup.transform.SetParent(root.transform, false);
                    pickup.transform.SetPositionAndRotation(center + WeaponOffsets[i], Quaternion.Euler(0f, 60f + 45f * i, 0f));
                }

                enemyNote = PlaceEnemies(scene, config);

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedHere && !Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
            }

            return $"確認用シーンに仮アイテムを {Offsets.Length} 個と武器を置いた: {PlayerTestSceneBuilder.ScenePath}（{note}）{enemyNote}";
        }

        private static string PlaceEnemies(Scene scene, FloorConfig config)
        {
            if (!PlayerTestSceneBuilder.TryFindBeyondEntrance(config, out Vector3 center, out Vector3 toward))
                return "\n  入口の部屋に扉が無いので敵は置かなかった。";

            var root = new GameObject(EnemiesRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Quaternion facing = Quaternion.LookRotation(toward, Vector3.up);
            int placed = 0;
            for (int i = 0; i < EnemyOffsets.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabs[i % EnemyPrefabs.Length]);
                if (prefab == null) continue;

                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                enemy.transform.SetParent(root.transform, false);
                enemy.transform.SetPositionAndRotation(center + facing * EnemyOffsets[i], facing);
                placed++;
            }

            return $"\n  扉の先 {center} に敵を {placed} 体置いた。";
        }
    }
}
