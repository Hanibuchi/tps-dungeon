using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpsDungeon.Items.Tests
{
    /// <summary>エンチャントの付き方（数・ランク・種類と段）と、個体でのまとめ方を確かめる。</summary>
    public sealed class EnchantmentRollerTests
    {
        private static readonly float[] EvenWeights = { 1f, 1f, 1f, 1f, 1f, 1f };

        private static EnchantmentRoller.Option[] Options(params (int kind, int rank)[] rows)
        {
            var options = new EnchantmentRoller.Option[rows.Length];
            for (int i = 0; i < rows.Length; i++) options[i] = new EnchantmentRoller.Option(rows[i].kind, rows[i].rank);
            return options;
        }

        [Test]
        public void 付く数は外れるまで振った回数で_最大数で切れる()
        {
            const float chance = 0.6f;
            const int trials = 20000;
            var random = new System.Random(3);

            int none = 0;
            int max = 0;
            int atCap = 0;
            for (int i = 0; i < trials; i++)
            {
                int count = EnchantmentRoller.RollRanks(chance, EvenWeights, 5, random).Count;
                if (count == 0) none++;
                if (count == 5) atCap++;
                max = Math.Max(max, count);
            }

            Assert.AreEqual(5, max);
            Assert.AreEqual(1.0 - chance, (double)none / trials, 0.015, "1 個も付かない割合");
            Assert.AreEqual(Math.Pow(chance, 5), (double)atCap / trials, 0.015, "5 回以上当たる割合");
        }

        [Test]
        public void 確率0か重みが無ければ付かない()
        {
            var random = new System.Random(2);
            Assert.IsEmpty(EnchantmentRoller.RollRanks(0f, EvenWeights, 5, random));
            Assert.IsEmpty(EnchantmentRoller.RollRanks(0.9f, new float[6], 5, random));
            Assert.IsEmpty(EnchantmentRoller.Roll(Options(), 0.9f, EvenWeights, 5, random));
        }

        [Test]
        public void ランクは重みの比で決まる()
        {
            var weights = new[] { 3f, 0f, 1f, 0f, 0f, 0f };
            var random = new System.Random(4);
            int e = 0, c = 0, other = 0;
            for (int i = 0; i < 20000; i++)
            {
                foreach (int rank in EnchantmentRoller.RollRanks(0.5f, weights, 64, random))
                {
                    if (rank == 0) e++;
                    else if (rank == 2) c++;
                    else other++;
                }
            }

            Assert.AreEqual(0, other);
            Assert.AreEqual(0.25, (double)c / (e + c), 0.015);
        }

        [Test]
        public void 最大数を超えたらランクの高いものが残る()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                // 同じシードなら振る回数もランクも同じ。上限で切った結果は、切らない結果の高い方から 5 個に一致する。
                List<int> all = EnchantmentRoller.RollRanks(0.9f, EvenWeights, 64, new System.Random(seed));
                List<int> capped = EnchantmentRoller.RollRanks(0.9f, EvenWeights, 5, new System.Random(seed));

                for (int i = 1; i < all.Count; i++) Assert.That(all[i - 1], Is.GreaterThanOrEqualTo(all[i]), "高い順に並ぶ");
                Assert.AreEqual(all.GetRange(0, Math.Min(5, all.Count)), capped);
            }
        }

        [Test]
        public void 同じ種類は段違いでも2つ付かない()
        {
            // 種類 0 の 1 段（E）・2 段（C）・3 段（S）と、種類 1 の 1 段（E）。
            EnchantmentRoller.Option[] options = Options((0, 0), (0, 2), (0, 5), (1, 0));
            var random = new System.Random(7);
            for (int trial = 0; trial < 2000; trial++)
            {
                List<int> chosen = EnchantmentRoller.Roll(options, 0.9f, EvenWeights, 5, random);
                Assert.That(chosen.Count, Is.LessThanOrEqualTo(2));
                var kinds = new HashSet<int>();
                foreach (int index in chosen) Assert.IsTrue(kinds.Add(options[index].Kind), "同じ種類が 2 つ付いた");
            }
        }

        [Test]
        public void そのランクに無ければ下で最も高いランクから選び_無ければ付かない()
        {
            // B と D だけの表。A を引けば B に、C を引けば D に落ち、E を引けば何も付かない。
            EnchantmentRoller.Option[] options = Options((0, 3), (1, 1));
            var random = new System.Random(8);

            // A だけが出る重み → 1 個目は B（種類 0）、2 個目も A → B は使い切ったので D（種類 1）。
            var onlyA = new[] { 0f, 0f, 0f, 0f, 1f, 0f };
            for (int trial = 0; trial < 500; trial++)
            {
                List<int> chosen = EnchantmentRoller.Roll(options, 0.7f, onlyA, 5, random);
                if (chosen.Count >= 1) Assert.AreEqual(0, chosen[0]);
                if (chosen.Count >= 2) Assert.AreEqual(1, chosen[1]);
            }

            var onlyE = new[] { 1f, 0f, 0f, 0f, 0f, 0f };
            for (int trial = 0; trial < 500; trial++) Assert.IsEmpty(EnchantmentRoller.Roll(options, 0.9f, onlyE, 5, random));
        }

        [Test]
        public void ランクの中では等確率に選ぶ()
        {
            EnchantmentRoller.Option[] options = Options((0, 2), (1, 2), (2, 2));
            var onlyC = new[] { 0f, 0f, 1f, 0f, 0f, 0f };
            var counts = new int[3];
            var random = new System.Random(9);
            for (int trial = 0; trial < 30000; trial++)
            {
                List<int> chosen = EnchantmentRoller.Roll(options, 0.5f, onlyC, 1, random);
                if (chosen.Count == 1) counts[chosen[0]]++;
            }

            int total = counts[0] + counts[1] + counts[2];
            foreach (int c in counts) Assert.AreEqual(1.0 / 3, (double)c / total, 0.02);
        }

        [Test]
        public void 個体は同じ種類を1つにまとめて段にし_効果は1段の値に段を掛ける()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var damage = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            var size = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            try
            {
                SetEnchantment(damage, EnchantmentKind.DamageUp, 0.2f);
                SetEnchantment(size, EnchantmentKind.Size, 0.2f);

                var instance = new ItemInstance(item, new[] { damage, size, damage, null });

                Assert.AreEqual(2, instance.Enchantments.Count);
                Assert.AreSame(damage, instance.Enchantments[0].Definition);
                Assert.AreEqual(2, instance.Enchantments[0].Level);
                Assert.AreEqual(1, instance.Enchantments[1].Level);

                EnchantmentTotals totals = instance.EnchantmentTotals();
                Assert.AreEqual(0.4f, totals.Amount(EnchantmentKind.DamageUp), 1e-5f);
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
        public void 武器種のランク表から種類と段が付き_同じ種類は重ならない()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var type = ScriptableObject.CreateInstance<WeaponTypeDefinition>();
            var settings = ScriptableObject.CreateInstance<EnchantmentRollSettings>();
            var damage = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            var size = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            try
            {
                SetEnchantment(damage, EnchantmentKind.DamageUp, 0.2f);
                SetEnchantment(size, EnchantmentKind.Size, 0.2f);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EnchantmentRollSettings).GetField("continueChance", flags).SetValue(settings, 0.9f);
                typeof(WeaponTypeDefinition).GetField("enchantmentRoll", flags).SetValue(type, settings);
                typeof(WeaponTypeDefinition).GetField("enchantmentRanks", flags).SetValue(type, new List<EnchantmentRankEntry>
                {
                    new EnchantmentRankEntry(damage, 1, WeaponRank.E),
                    new EnchantmentRankEntry(damage, 2, WeaponRank.C),
                    new EnchantmentRankEntry(size, 3, WeaponRank.A),
                });
                SetWeapon(weapon, WeaponRank.B);
                typeof(WeaponDefinition).GetField("weaponType", flags).SetValue(weapon, type);

                bool sawTwo = false;
                for (int seed = 0; seed < 300; seed++)
                {
                    ItemInstance instance = ItemInstance.Create(weapon, new System.Random(seed));
                    Assert.That(instance.Enchantments.Count, Is.LessThanOrEqualTo(2));
                    foreach (EnchantmentStack stack in instance.Enchantments)
                    {
                        if (stack.Definition == damage) Assert.That(stack.Level, Is.EqualTo(1).Or.EqualTo(2));
                        else Assert.AreEqual(3, stack.Level);
                    }

                    if (instance.Enchantments.Count == 2)
                    {
                        sawTwo = true;
                        Assert.AreNotSame(instance.Enchantments[0].Definition, instance.Enchantments[1].Definition);
                    }
                }

                Assert.IsTrue(sawTwo);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(type);
                Object.DestroyImmediate(settings);
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

        [Test]
        public void ユニークは振らずに固定のエンチャントが書いた段で付く()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var duration = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            var multishot = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            try
            {
                SetEnchantment(duration, EnchantmentKind.Duration, 0.2f);
                SetEnchantment(multishot, EnchantmentKind.Multishot, 1f);
                SetWeapon(weapon, WeaponRank.Unique, new FixedEnchantmentEntry(duration, 2), new FixedEnchantmentEntry(multishot, 1));

                for (int seed = 0; seed < 20; seed++)
                {
                    ItemInstance instance = ItemInstance.Create(weapon, new System.Random(seed));
                    Assert.AreEqual(2, instance.Enchantments.Count);
                    Assert.AreSame(duration, instance.Enchantments[0].Definition);
                    Assert.AreEqual(2, instance.Enchantments[0].Level);
                    Assert.AreSame(multishot, instance.Enchantments[1].Definition);
                    Assert.AreEqual(1, instance.Enchantments[1].Level);
                }
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(duration);
                Object.DestroyImmediate(multishot);
            }
        }

        [Test]
        public void ユニーク以外では固定のエンチャントを使わない()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var duration = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            try
            {
                SetEnchantment(duration, EnchantmentKind.Duration, 0.2f);
                SetWeapon(weapon, WeaponRank.S, new FixedEnchantmentEntry(duration, 1));

                // 武器種が無いので振る候補も無く、何も付かない。
                Assert.IsEmpty(ItemInstance.Create(weapon, new System.Random(6)).Enchantments);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(duration);
            }
        }

        private static void SetWeapon(WeaponDefinition weapon, WeaponRank rank, params FixedEnchantmentEntry[] fixedEnchantments)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(WeaponDefinition).GetField("rank", flags).SetValue(weapon, rank);
            typeof(WeaponDefinition).GetField("fixedEnchantments", flags)
                .SetValue(weapon, new List<FixedEnchantmentEntry>(fixedEnchantments));
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
