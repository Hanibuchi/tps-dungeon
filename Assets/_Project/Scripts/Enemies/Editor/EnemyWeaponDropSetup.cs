using TpsDungeon.Items;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵のプレハブに武器のドロップ（EnemyWeaponDrop）と、ドロップ表で引く id（EnemyIdentity）を付ける。何度実行しても同じ結果になる。
    /// MonsterBuilder で作り直したプレハブには MonsterBuilder が付けるので、既存のプレハブを直すときだけ使う。
    /// </summary>
    public static class EnemyWeaponDropSetup
    {
        private const string PrefabFolder = EnemyIdentitySetup.PrefabFolder;

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

                    var identity = root.GetComponent<EnemyIdentity>();
                    if (identity == null) identity = root.AddComponent<EnemyIdentity>();
                    EnemyIdentitySetup.Assign(identity, EnemyIdentitySetup.IdFromPrefabName(System.IO.Path.GetFileNameWithoutExtension(path)));
                    if (root.GetComponent<EnemyWeaponDrop>() == null) root.AddComponent<EnemyWeaponDrop>();
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
    }
}
