using TpsDungeon.Items;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵のプレハブに武器のドロップ（EnemyWeaponDrop）を付け、ドロップ表の id にプレハブのファイル名を入れる。何度実行しても同じ結果になる。
    /// MonsterBuilder で作り直したプレハブには MonsterBuilder が付けるので、既存のプレハブを直すときだけ使う。
    /// </summary>
    public static class EnemyWeaponDropSetup
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";

        [MenuItem("Tools/TPS Dungeon/Enemies/敵に武器のドロップを付ける")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<EnemyHealth>() == null) continue;

                    var drop = root.GetComponent<EnemyWeaponDrop>();
                    if (drop == null) drop = root.AddComponent<EnemyWeaponDrop>();
                    Assign(drop, System.IO.Path.GetFileNameWithoutExtension(path));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return $"敵 {count} 体に武器のドロップを付けた";
        }

        public static void Assign(EnemyWeaponDrop drop, string enemyId)
        {
            var serialized = new SerializedObject(drop);
            serialized.FindProperty("enemyId").stringValue = enemyId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
