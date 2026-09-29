using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpsDungeon.Items.Tests
{
    /// <summary>エンチャントの付き方と、個体でのまとめ方を確かめる。</summary>
    public sealed class EnchantmentRollerTests
    {
        [Test]
        public void 候補の外の番号は出ない()
        {
            var random = new System.Random(1);
            for (int trial = 0; trial < 1000; trial++)
            {
                foreach (int index in EnchantmentRoller.Roll(9, 0.8f, random))
                {
                    Assert.That(index, Is.InRange(0, 8));
                }
            }
        }

        [Test]
        public void 確率0か候補が無ければ付かない()
        {
            var random = new System.Random(2);
            Assert.IsEmpty(EnchantmentRoller.Roll(9, 0f, random));
            Assert.IsEmpty(EnchantmentRoller.Roll(0, 0.9f, random));
        }

        [Test]
        public void 付く数の平均は幾何分布になる()
        {
            const float chance = 0.35f;
            const int trials = 20000;
            var random = new System.Random(3);

            long total = 0;
            int none = 0;
            for (int i = 0; i < trials; i++)
            {
                int count = EnchantmentRoller.Roll(9, chance, random).Count;
                total += count;
                if (count == 0) none++;
            }

            Assert.AreEqual(EnchantmentRoller.ExpectedCount(chance), (double)total / trials, 0.03);
            Assert.AreEqual(1.0 - chance, (double)none / trials, 0.015, "1 個も付かない割合");
        }

        [Test]
        public void 上限を決めずに何個でも付きうる()
        {
            var random = new System.Random(4);
            int max = 0;
            for (int i = 0; i < 20000; i++) max = Math.Max(max, EnchantmentRoller.Roll(9, 0.35f, random).Count);

            Assert.GreaterOrEqual(max, 5);
        }

        [Test]
        public void 個体は同じ種類をまとめて数え_効果を足し合わせる()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var damage = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            var size = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            try
            {
                SetEnchantment(damage, EnchantmentKind.DamageUp, 0.15f);
                SetEnchantment(size, EnchantmentKind.Size, 0.2f);

                var instance = new ItemInstance(item, new[] { damage, size, damage, null });

                Assert.AreEqual(2, instance.Enchantments.Count);
                Assert.AreSame(damage, instance.Enchantments[0].Definition);
                Assert.AreEqual(2, instance.Enchantments[0].Count);
                Assert.AreEqual(1, instance.Enchantments[1].Count);

                EnchantmentTotals totals = instance.EnchantmentTotals();
                Assert.AreEqual(0.3f, totals.Amount(EnchantmentKind.DamageUp), 1e-5f);
                Assert.AreEqual(2, totals.Stacks(EnchantmentKind.DamageUp));
                Assert.AreEqual(0.2f, totals.Amount(EnchantmentKind.Size), 1e-5f);
                Assert.IsFalse(totals.Has(EnchantmentKind.Explosion));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(damage);
                Object.DestroyImmediate(size);
            }
        }

        [Test]
        public void 武器でない定義からはエンチャントの無い個体ができる()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                ItemInstance instance = ItemInstance.Create(item, new System.Random(5));
                Assert.AreSame(item, instance.Definition);
                Assert.IsEmpty(instance.Enchantments);
                Assert.IsNull(instance.Weapon);
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        /// <summary>テストのためだけに公開の口を増やさず、シリアライズされる private フィールドへ直接入れる。</summary>
        private static void SetEnchantment(EnchantmentDefinition definition, EnchantmentKind kind, float amount)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(EnchantmentDefinition).GetField("kind", flags).SetValue(definition, kind);
            typeof(EnchantmentDefinition).GetField("amount", flags).SetValue(definition, amount);
        }
    }
}
