using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TpsDungeon.Items.Tests
{
    /// <summary>持ち物をセーブの形にして戻す（ItemSaveData）と、id の表（ItemCatalog）を確かめる。</summary>
    public sealed class ItemSaveTests
    {
        private readonly List<Object> created = new List<Object>();
        private ItemCatalog catalog;
        private ItemDefinition sword;
        private ItemDefinition potion;
        private EnchantmentDefinition damage;
        private EnchantmentDefinition speed;

        [SetUp]
        public void SetUp()
        {
            sword = Item("sword");
            potion = Item("potion");
            damage = Enchantment("Enchant_Damage");
            speed = Enchantment("Enchant_Speed");
            catalog = Make<ItemCatalog>();
            catalog.Set(new[] { sword, potion }, new[] { damage, speed });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void エンチャントの種類と段を保ったまま戻る()
        {
            var item = new ItemInstance(sword, new[] { new EnchantmentStack(damage, 2), new EnchantmentStack(speed, 1) });

            ItemSaveData data = ItemSaveData.From(item);
            string json = JsonUtility.ToJson(data);
            ItemInstance back = JsonUtility.FromJson<ItemSaveData>(json).ToInstance(catalog);

            Assert.AreSame(sword, back.Definition);
            Assert.AreEqual(2, back.Enchantments.Count);
            Assert.AreSame(damage, back.Enchantments[0].Definition);
            Assert.AreEqual(2, back.Enchantments[0].Level);
            Assert.AreSame(speed, back.Enchantments[1].Definition);
            Assert.AreEqual(1, back.Enchantments[1].Level);
        }

        [Test]
        public void 空き枠はnullに戻る()
        {
            ItemSaveData data = ItemSaveData.From(null);
            Assert.IsTrue(data.IsEmpty);
            Assert.IsNull(data.ToInstance(catalog));
        }

        [Test]
        public void 表に無いアイテムは捨て_表に無いエンチャントは外す()
        {
            var unknown = new ItemSaveData { id = "nothing" };
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("nothing"));
            Assert.IsNull(unknown.ToInstance(catalog));

            var partly = new ItemSaveData { id = "sword" };
            partly.enchantments.Add(new EnchantmentSaveData { id = "Enchant_Damage", level = 1 });
            partly.enchantments.Add(new EnchantmentSaveData { id = "Enchant_Gone", level = 3 });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Enchant_Gone"));
            ItemInstance back = partly.ToInstance(catalog);
            Assert.AreEqual(1, back.Enchantments.Count);
            Assert.AreSame(damage, back.Enchantments[0].Definition);
        }

        [Test]
        public void 枠の並びを空き枠ごと戻し_今の中身は消す()
        {
            var source = new Inventory(4, 0);
            source.Place(1, new ItemInstance(sword));
            source.Place(3, new ItemInstance(potion));
            List<ItemSaveData> slots = ItemSaveData.FromSlots(source);

            var target = new Inventory(4, 0);
            target.Place(0, new ItemInstance(potion));
            ItemSaveData.RestoreSlots(target, slots, catalog);

            Assert.IsNull(target[0]);
            Assert.AreSame(sword, target[1].Definition);
            Assert.IsNull(target[2]);
            Assert.AreSame(potion, target[3].Definition);
        }

        [Test]
        public void バッグは保存の方が多ければ枠を広げて入れる()
        {
            var source = new Inventory(0, 8);
            source.Place(7, new ItemInstance(sword));
            List<ItemSaveData> slots = ItemSaveData.FromSlots(source);

            var target = new Inventory(0, 6);
            ItemSaveData.RestoreSlots(target, slots, catalog);

            Assert.AreEqual(8, target.Count);
            Assert.AreSame(sword, target[7].Definition);
        }

        [Test]
        public void ホットバーだけの手持ちは広げず_入りきらない分は捨てる()
        {
            var slots = new List<ItemSaveData>();
            for (int i = 0; i < 6; i++) slots.Add(new ItemSaveData { id = "potion" });

            var target = new Inventory(4, 0);
            ItemSaveData.RestoreSlots(target, slots, catalog);

            Assert.AreEqual(4, target.Count);
        }

        [Test]
        public void ゲームのid表に_Itemsにあるアイテムとエンチャントが全部入っていて_idが空でも重なってもいない()
        {
            ItemCatalog table = ItemCatalog.Default;
            Assert.IsNotNull(table, $"Resources/{ItemCatalog.ResourcePath} が無い（「アイテムの id の表を作り直す」を実行する）");

            List<ItemDefinition> items = Load<ItemDefinition>();
            List<EnchantmentDefinition> enchantments = Load<EnchantmentDefinition>();
            CollectionAssert.IsSubsetOf(items, table.Items.ToList(), "表に無いアイテムがある（作り直す）");
            CollectionAssert.IsSubsetOf(enchantments, table.Enchantments.ToList(), "表に無いエンチャントがある（作り直す）");

            Assert.IsEmpty(items.Where(i => string.IsNullOrEmpty(i.Id)).Select(i => i.name), "id が空のアイテム");
            Assert.IsEmpty(items.GroupBy(i => i.Id).Where(g => g.Count() > 1).Select(g => g.Key), "id が重なっているアイテム");
            Assert.IsEmpty(enchantments.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key), "名前が重なっているエンチャント");
        }

        private static List<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_Project/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();

        private ItemDefinition Item(string id)
        {
            var item = Make<ItemDefinition>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private EnchantmentDefinition Enchantment(string id)
        {
            var enchantment = Make<EnchantmentDefinition>();
            enchantment.name = id;
            return enchantment;
        }

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }
    }
}
