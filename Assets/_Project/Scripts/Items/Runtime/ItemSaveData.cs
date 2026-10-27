using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>付いたエンチャント 1 種類の保存の形（<see cref="EnchantmentDefinition.Id"/> と段）。</summary>
    [Serializable]
    public sealed class EnchantmentSaveData
    {
        public string id;
        public int level;
    }

    /// <summary>持ち物の枠 1 つの保存の形。id が空なら空き枠。JsonUtility で書き出せる形にしてある。</summary>
    [Serializable]
    public sealed class ItemSaveData
    {
        /// <summary><see cref="ItemDefinition.Id"/>。空なら空き枠。</summary>
        public string id;
        public List<EnchantmentSaveData> enchantments = new List<EnchantmentSaveData>();

        public bool IsEmpty => string.IsNullOrEmpty(id);

        /// <summary>個体を保存の形にする。null なら空き枠。</summary>
        public static ItemSaveData From(ItemInstance item)
        {
            var data = new ItemSaveData();
            if (item == null) return data;

            data.id = item.Definition.Id;
            foreach (EnchantmentStack stack in item.Enchantments)
            {
                if (stack.Definition == null) continue;
                data.enchantments.Add(new EnchantmentSaveData { id = stack.Definition.Id, level = stack.Level });
            }
            return data;
        }

        /// <summary>
        /// 個体に戻す（エンチャントは振り直さず、保存したものを付ける）。空き枠か、表に無いアイテムなら null。
        /// 表に無いエンチャントは落とす。どちらも警告を出す。
        /// </summary>
        public ItemInstance ToInstance(ItemCatalog catalog)
        {
            if (IsEmpty) return null;
            if (catalog == null || !catalog.TryGetItem(id, out ItemDefinition definition))
            {
                Debug.LogWarning($"保存にあるアイテム \"{id}\" が見つからないので捨てた");
                return null;
            }

            var stacks = new List<EnchantmentStack>();
            if (enchantments != null)
            {
                foreach (EnchantmentSaveData entry in enchantments)
                {
                    if (entry == null || entry.level <= 0) continue;
                    if (catalog.TryGetEnchantment(entry.id, out EnchantmentDefinition enchantment)) stacks.Add(new EnchantmentStack(enchantment, entry.level));
                    else Debug.LogWarning($"保存にあるエンチャント \"{entry.id}\" が見つからないので {id} から外した");
                }
            }
            return new ItemInstance(definition, stacks);
        }

        /// <summary>枠の並びを丸ごと保存の形にする（空き枠も並びのまま残す）。</summary>
        public static List<ItemSaveData> FromSlots(Inventory inventory)
        {
            var result = new List<ItemSaveData>();
            if (inventory == null) return result;
            for (int i = 0; i < inventory.Count; i++) result.Add(From(inventory[i]));
            return result;
        }

        /// <summary>
        /// 枠の並びを丸ごと戻す。今の中身は消す。保存の方が枠が多ければ、入りきらない分はバッグの枠を広げて入れる
        /// （ホットバーだけの手持ちなら、入りきらない分は捨てる）。
        /// </summary>
        public static void RestoreSlots(Inventory inventory, IReadOnlyList<ItemSaveData> slots, ItemCatalog catalog)
        {
            if (inventory == null) return;
            for (int i = 0; i < inventory.Count; i++) inventory.Place(i, null);
            if (slots == null) return;

            int last = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty) last = i;
            }
            if (last >= inventory.Count && inventory.BagCapacity > 0) inventory.ExpandBag(last + 1 - inventory.Count);

            for (int i = 0; i < slots.Count && i < inventory.Count; i++)
            {
                if (slots[i] != null) inventory.Place(i, slots[i].ToInstance(catalog));
            }
        }
    }
}
