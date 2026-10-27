using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// セーブから持ち物を戻すための id の表（<see cref="ItemCatalog"/>）を書く。
    /// Assets/_Project/Items 以下の ItemDefinition と EnchantmentDefinition を全部集め、Resources/Items/ItemCatalog に置く。
    /// アイテムの生成器（アイテム・武器）の最後でも呼ぶ。id が空・重なっているものがあれば警告を出す（そのアイテムはセーブから戻らない）。
    /// </summary>
    public static class ItemCatalogBuilder
    {
        public const string CatalogPath = "Assets/_Project/Resources/" + ItemCatalog.ResourcePath + ".asset";

        [MenuItem("Tools/TPS Dungeon/素材・プレハブ/アイテムの id の表を作り直す", false, 523)]
        public static void Build()
        {
            List<ItemDefinition> items = FindAll<ItemDefinition>();
            List<EnchantmentDefinition> enchantments = FindAll<EnchantmentDefinition>();
            foreach (string problem in Problems(items, enchantments)) Debug.LogWarning($"アイテムの id の表: {problem}");

            PlaceholderItemAssetGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Set(items, enchantments);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"アイテムの id の表を作った: アイテム {items.Count}・エンチャント {enchantments.Count}（{CatalogPath}）");
        }

        /// <summary>Assets/_Project/Items 以下の T を、パスの順に全部。</summary>
        public static List<T> FindAll<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { PlaceholderItemAssetGenerator.ItemsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        /// <summary>id が空・重なっているもの。</summary>
        public static List<string> Problems(IEnumerable<ItemDefinition> items, IEnumerable<EnchantmentDefinition> enchantments)
        {
            var problems = new List<string>();
            var itemIds = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in items)
            {
                if (string.IsNullOrEmpty(item.Id)) problems.Add($"{item.name} の id が空");
                else if (itemIds.TryGetValue(item.Id, out ItemDefinition other)) problems.Add($"id \"{item.Id}\" が {other.name} と {item.name} で重なっている");
                else itemIds.Add(item.Id, item);
            }

            var enchantmentIds = new Dictionary<string, EnchantmentDefinition>();
            foreach (EnchantmentDefinition enchantment in enchantments)
            {
                if (enchantmentIds.TryGetValue(enchantment.Id, out EnchantmentDefinition other)) problems.Add($"エンチャントの名前 \"{enchantment.Id}\" が重なっている（{AssetDatabase.GetAssetPath(other)} と {AssetDatabase.GetAssetPath(enchantment)}）");
                else enchantmentIds.Add(enchantment.Id, enchantment);
            }
            return problems;
        }
    }
}
