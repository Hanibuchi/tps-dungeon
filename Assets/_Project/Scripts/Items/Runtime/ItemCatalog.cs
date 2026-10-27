using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// セーブから持ち物を戻すときに、id からアイテムとエンチャントの定義を引く表。
    /// アイテムは <see cref="ItemDefinition.Id"/>、エンチャントは <see cref="EnchantmentDefinition.Id"/>（アセット名）で引く。
    /// ゲーム全体で 1 つだけ使い、Resources/<see cref="ResourcePath"/> に置く（<see cref="Default"/> で引く）。
    /// 中身は ItemCatalogBuilder（エディタ）が Assets/_Project/Items から集めて書く。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "TPS Dungeon/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        /// <summary>Resources から読むときのパス（拡張子なし）。</summary>
        public const string ResourcePath = "Items/ItemCatalog";

        private static ItemCatalog cached;

        [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();
        [SerializeField] private EnchantmentDefinition[] enchantments = Array.Empty<EnchantmentDefinition>();

        private Dictionary<string, ItemDefinition> itemsById;
        private Dictionary<string, EnchantmentDefinition> enchantmentsById;

        /// <summary>ゲーム全体で使う表。Resources に無ければ null。</summary>
        public static ItemCatalog Default
        {
            get
            {
                if (cached == null) cached = Resources.Load<ItemCatalog>(ResourcePath);
                return cached;
            }
        }

        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<EnchantmentDefinition> Enchantments => enchantments;

        /// <summary>中身を差し替える（ビルダーとテスト用）。</summary>
        public void Set(IEnumerable<ItemDefinition> newItems, IEnumerable<EnchantmentDefinition> newEnchantments)
        {
            items = newItems != null ? new List<ItemDefinition>(newItems).ToArray() : Array.Empty<ItemDefinition>();
            enchantments = newEnchantments != null ? new List<EnchantmentDefinition>(newEnchantments).ToArray() : Array.Empty<EnchantmentDefinition>();
            itemsById = null;
            enchantmentsById = null;
        }

        public bool TryGetItem(string id, out ItemDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id)) return false;
            itemsById ??= Index(items, d => d.Id);
            return itemsById.TryGetValue(id, out definition);
        }

        public bool TryGetEnchantment(string id, out EnchantmentDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id)) return false;
            enchantmentsById ??= Index(enchantments, d => d.Id);
            return enchantmentsById.TryGetValue(id, out definition);
        }

        private void OnValidate()
        {
            itemsById = null;
            enchantmentsById = null;
        }

        // 同じ id が並んでいたら先の方を使う（ビルダーが警告を出す）。
        private static Dictionary<string, T> Index<T>(IEnumerable<T> source, Func<T, string> idOf) where T : UnityEngine.Object
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T entry in source)
            {
                if (entry == null) continue;
                string id = idOf(entry);
                if (!string.IsNullOrEmpty(id) && !result.ContainsKey(id)) result.Add(id, entry);
            }
            return result;
        }
    }
}
