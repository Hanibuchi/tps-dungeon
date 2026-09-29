using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵が死んで透明になって消えるための準備。
    /// 半透明の URP Lit 材質（雛形）を作り、敵のプレハブの EnemyDeath に入れる。
    /// 雛形をプレハブから参照しておくと、ビルドにも半透明の変種が入る。何度実行しても同じ結果になる。
    /// MonsterBuilder で作り直したプレハブには MonsterBuilder が入れるので、既存のプレハブを直すときだけ使う。
    /// </summary>
    public static class EnemyDeathSetup
    {
        public const string FadeTemplatePath = "Assets/_Project/Materials/Enemies/EnemyFade.mat";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";

        [MenuItem("Tools/TPS Dungeon/Enemies/敵の死に方を設定")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            Material template = EnsureFadeTemplate();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var death = root.GetComponent<EnemyDeath>();
                    if (death == null) continue;

                    Assign(death, template);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return $"敵 {count} 体に消え方の雛形を入れた: {FadeTemplatePath}";
        }

        public static void Assign(EnemyDeath death, Material template)
        {
            var serialized = new SerializedObject(death);
            serialized.FindProperty("fadeTemplate").objectReferenceValue = template;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>半透明の URP Lit 材質を作る（あれば設定だけ入れ直す）。</summary>
        public static Material EnsureFadeTemplate()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FadeTemplatePath);
            if (material == null)
            {
                EnsureFolder(FadeTemplatePath.Substring(0, FadeTemplatePath.LastIndexOf('/')));
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = "EnemyFade" };
                AssetDatabase.CreateAsset(material, FadeTemplatePath);
            }

            EnemyDeath.MakeTransparent(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
        }
    }
}
