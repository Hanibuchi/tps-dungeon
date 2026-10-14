using System.Linq;
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

        /// <summary>敵種（Notion「敵種」）ごとに 1 体ずつ。01 は大小 2 体。</summary>
        private static readonly string[] EnemyPrefabs =
        {
            "Assets/_Project/Prefabs/Enemies/Big_Orc.prefab",          // 01 単純近距離
            "Assets/_Project/Prefabs/Enemies/Blob_GreenBlob.prefab",   // 01 単純近距離（小）
            "Assets/_Project/Prefabs/Enemies/Big_Frog.prefab",         // 02 飛びつき近距離
            "Assets/_Project/Prefabs/Enemies/Flying_Ghost.prefab",     // 03 浮遊近距離
            "Assets/_Project/Prefabs/Enemies/Blob_PinkBlob.prefab",    // 04 自爆
            "Assets/_Project/Prefabs/Enemies/Blob_Mushnub.prefab",     // 05 毒霧
            "Assets/_Project/Prefabs/Enemies/Flying_Hywirl.prefab",    // 06 浮遊遠距離
            "Assets/_Project/Prefabs/Enemies/Blob_Wizard.prefab",      // 07 遠距離
        };

        /// <summary>扉の先のセルの中心から見た敵の置き場所（入口へ向かう向きを +Z とする）。近接を手前、遠距離を奥に、セル（5m）の内側に 2 列で並べる。</summary>
        private static readonly Vector3[] EnemyOffsets =
        {
            new Vector3(-1.8f, 0f, 0.6f),
            new Vector3(-0.6f, 0f, 0.6f),
            new Vector3(0.6f, 0f, 0.6f),
            new Vector3(1.8f, 0f, 0.6f),
            new Vector3(-1.8f, 0f, -1.2f),
            new Vector3(-0.6f, 0f, -1.2f),
            new Vector3(0.6f, 0f, -1.2f),
            new Vector3(1.8f, 0f, -1.2f),
        };

        // 以下の置き場所は、入口の部屋の FeatureCell 中心から見た「下り階段のローカル」の座標。部屋の回転に合わせて回して置く。
        // 階段のローカルでは、ドアが -Z の壁の真ん中、下り階段の穴がドアの正面（x = ±0.7）の z = -0.9〜2.3、
        // スポーン地点が (1.6, -0.3) にある。穴（|x| < 0.7、少し余裕を見て 0.85）と、扉板の開く範囲（ドアの壁から 1.5 m、|x| < 1.5）を避ける。

        /// <summary>仮アイテムの置き場所。穴の西側の壁際に縦に並べる。ホットバー 4 枠を越えてバッグに入るのを試せる数にする。</summary>
        private static readonly Vector3[] Offsets =
        {
            new Vector3(-1.9f, 0f, -1.9f),
            new Vector3(-1.9f, 0f, -0.6f),
            new Vector3(-1.9f, 0f, 0.7f),
            new Vector3(-1.9f, 0f, 2.0f),
        };

        /// <summary>仮の武器の置き場所。西側のもう 1 列と、スポーン地点のある東側に分けて、すぐ拾えるようにする。</summary>
        private static readonly Vector3[] WeaponOffsets =
        {
            new Vector3(-1.2f, 0f, -0.6f),
            new Vector3(-1.2f, 0f, 0.7f),
            new Vector3(-1.2f, 0f, 2.0f),
            // ダッシュ突き・両手近距離・叩きつけ。
            new Vector3(1.9f, 0f, -1.9f),
            new Vector3(1.9f, 0f, 1.0f),
            new Vector3(1.9f, 0f, 2.0f),
        };

        /// <summary>お守りと盾を 1 つずつ置く場所（片手剣と一緒に試せるよう、東側の武器の隣）。</summary>
        private static readonly Vector3[] GearOffsets =
        {
            new Vector3(1.2f, 0f, 1.0f),
            new Vector3(1.2f, 0f, 2.0f),
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
            if (!PlayerTestSceneBuilder.TryFindEntrance(config, out Vector3 center, out int rotation, out string note))
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

                // 置き場所は階段のローカルなので、入口の部屋（＝階段）と同じだけ回す。
                Quaternion turn = Quaternion.Euler(0f, 90f * rotation, 0f);

                for (int i = 0; i < Offsets.Length; i++)
                {
                    ItemDefinition item = items[i % items.Count];
                    var pickup = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldPrefab.gameObject, scene);
                    pickup.transform.SetParent(root.transform, false);
                    pickup.transform.SetPositionAndRotation(center + turn * Offsets[i], Quaternion.Euler(0f, 37f * i, 0f));
                }

                var weapons = PlaceholderWeaponAssetGenerator.LoadAll();
                for (int i = 0; i < weapons.Count && i < WeaponOffsets.Length; i++)
                {
                    if (weapons[i].WorldPrefab == null) continue;
                    var pickup = (GameObject)PrefabUtility.InstantiatePrefab(weapons[i].WorldPrefab.gameObject, scene);
                    pickup.transform.SetParent(root.transform, false);
                    pickup.transform.SetPositionAndRotation(center + turn * WeaponOffsets[i], Quaternion.Euler(0f, 60f + 45f * i, 0f));
                }

                // お守り・盾はいちばんランクの低いものを 1 つずつ。
                var gear = new[]
                {
                    weapons.Where(w => w.WeaponType != null && w.WeaponType.IsCharm).OrderBy(w => w.Rank).FirstOrDefault(),
                    weapons.Where(w => w.WeaponType != null && w.WeaponType.IsShield).OrderBy(w => w.Rank).FirstOrDefault(),
                };
                for (int i = 0; i < gear.Length && i < GearOffsets.Length; i++)
                {
                    if (gear[i] == null || gear[i].WorldPrefab == null) continue;
                    var pickup = (GameObject)PrefabUtility.InstantiatePrefab(gear[i].WorldPrefab.gameObject, scene);
                    pickup.transform.SetParent(root.transform, false);
                    pickup.transform.SetPositionAndRotation(center + turn * GearOffsets[i], Quaternion.Euler(0f, 30f + 50f * i, 0f));
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
