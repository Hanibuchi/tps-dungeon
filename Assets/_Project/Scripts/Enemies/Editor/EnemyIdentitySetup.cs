using System.Text;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵のプレハブに id（EnemyIdentity）を付ける。id はプレハブ名のモデルの部分の snake_case（Blob_PinkBlob → pink_blob）。
    /// 何度実行しても同じ結果になる。MonsterBuilder で作り直したプレハブには MonsterBuilder が付けるので、既存のプレハブを直すときだけ使う。
    /// </summary>
    public static class EnemyIdentitySetup
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";

        [MenuItem("Tools/TPS Dungeon/Enemies/敵に id を付ける")]
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
                    Assign(identity, IdFromPrefabName(System.IO.Path.GetFileNameWithoutExtension(path)));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return $"敵 {count} 体に id を付けた";
        }

        public static void Assign(EnemyIdentity identity, string id)
        {
            var serialized = new SerializedObject(identity);
            serialized.FindProperty("id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>「グループ_モデル」のプレハブ名から id を作る。Blob_PinkBlob → pink_blob、Big_Orc → orc。</summary>
        public static string IdFromPrefabName(string prefabName)
        {
            int underscore = prefabName.IndexOf('_');
            return ToSnakeCase(underscore >= 0 ? prefabName.Substring(underscore + 1) : prefabName);
        }

        /// <summary>PinkBlob → pink_blob。</summary>
        public static string ToSnakeCase(string name)
        {
            var builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0 && name[i - 1] != '_') builder.Append('_');
                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
    }
}
