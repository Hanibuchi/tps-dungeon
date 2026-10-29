using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Items.Tests
{
    /// <summary>お守りの鑑定で情報欄に足す行（実効 DPS・HPS・売値）と、売値の表と計算。</summary>
    public sealed class AppraisalTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            created.Add(o);
            return o;
        }

        private static void Set(object target, string field, object value)
        {
            System.Type type = target.GetType();
            FieldInfo info = null;
            while (type != null && (info = type.GetField(field, Private)) == null) type = type.BaseType;
            info.SetValue(target, value);
        }

        private WeaponDefinition Weapon(RangedAttackKind kind, PassiveGear gear = PassiveGear.None, WeaponRank rank = WeaponRank.C, int price = 0)
        {
            var type = Create<WeaponTypeDefinition>();
            Set(type, "rangedKind", kind);
            Set(type, "fireInterval", 1f);
            Set(type, "passiveGear", gear);
            if (kind == RangedAttackKind.Heal) Set(type, "supportTargetCount", 2);
            if (kind == RangedAttackKind.Summon) Set(type, "summonDuration", 12f);

            var weapon = Create<WeaponDefinition>();
            Set(weapon, "rank", rank);
            Set(weapon, "strength", 20f);
            Set(weapon, "weaponType", type);
            Set(weapon, "basePrice", price);
            return weapon;
        }

        private WeaponPriceTable Table()
        {
            var table = Create<WeaponPriceTable>();
            Set(table, "entries", new[]
            {
                new WeaponPriceTable.Entry { rank = WeaponRank.C, weapon = 60, enchantment = 20 },
                new WeaponPriceTable.Entry { rank = WeaponRank.A, weapon = 400, enchantment = 80 },
            });
            return table;
        }

        [Test]
        public void 鑑定が無ければ実効DPSも売値も出さない()
        {
            string text = new ItemInstance(Weapon(RangedAttackKind.Bow)).DetailText();
            StringAssert.DoesNotContain("実効", text);
            StringAssert.DoesNotContain("売値", text);
        }

        [Test]
        public void 鑑定1で攻撃力の次に実効DPSを_2で売値も出す()
        {
            var item = new ItemInstance(Weapon(RangedAttackKind.Bow));
            float dps = item.Weapon.EffectiveDps(item.EnchantmentTotals());

            string[] one = item.DetailText(1).Split('\n');
            StringAssert.StartsWith("攻撃力 ", one[1]);
            Assert.AreEqual($"実効 DPS {dps:0.#}", one[2]);
            StringAssert.DoesNotContain("売値", string.Join("\n", one));

            string[] two = item.DetailText(2).Split('\n');
            Assert.AreEqual(one[2], two[2]);
            Assert.AreEqual($"売値 {item.SellPrice()}", two[3]);
        }

        [Test]
        public void 回復の武器はHPSを出し_盾やお守りはDPSを出さず売値だけ出す()
        {
            var heal = new ItemInstance(Weapon(RangedAttackKind.Heal));
            string healText = heal.DetailText(2);
            StringAssert.Contains($"実効 HPS {heal.Weapon.HealPerSecond(heal.EnchantmentTotals()):0.#}", healText);
            StringAssert.DoesNotContain("DPS", healText);

            foreach (PassiveGear gear in new[] { PassiveGear.Shield, PassiveGear.Charm })
            {
                string text = new ItemInstance(Weapon(RangedAttackKind.None, gear)).DetailText(2);
                StringAssert.DoesNotContain("実効", text);
                StringAssert.Contains("\n売値 ", text);
            }

            string summon = new ItemInstance(Weapon(RangedAttackKind.Summon)).DetailText(2);
            StringAssert.DoesNotContain("実効", summon);
        }

        [Test]
        public void 売値は武器の金額とランクの金額とエンチャントのランクの金額の合計に倍率を掛ける()
        {
            WeaponDefinition weapon = Weapon(RangedAttackKind.Bow, rank: WeaponRank.A, price: 100);
            var damage = Create<EnchantmentDefinition>();
            Set(damage, "kind", EnchantmentKind.DamageUp);
            var crit = Create<EnchantmentDefinition>();
            Set(crit, "kind", EnchantmentKind.CritChance);
            Set(weapon.WeaponType, "enchantmentRanks", new List<EnchantmentRankEntry>
            {
                new EnchantmentRankEntry(damage, 1, WeaponRank.C),
                new EnchantmentRankEntry(crit, 2, WeaponRank.A),
            });

            var item = new ItemInstance(weapon, new[] { new EnchantmentStack(damage, 1), new EnchantmentStack(crit, 2) });
            WeaponPriceTable table = Table();
            Assert.AreEqual(100 + 400 + 20 + 80, item.SellPrice(1f, table));
            Assert.AreEqual(720, item.SellPrice(1.2f, table));
            Assert.AreEqual(100, new ItemInstance(Weapon(RangedAttackKind.Bow, rank: WeaponRank.B, price: 100)).SellPrice(1f, table), "表に無いランクは 0");
        }

        [Test]
        public void 売値の計算は四捨五入し負の金額は数えない()
        {
            Assert.AreEqual(0, WeaponPrice.Compute(0, 0, null));
            Assert.AreEqual(13, WeaponPrice.Compute(5, 5, new[] { 1 }, 1.15f), "11 × 1.15 = 12.65");
            Assert.AreEqual(10, WeaponPrice.Compute(-5, 10, new[] { -3 }));
        }

        [Test]
        public void 売値の表は見出し_空行_コメントを飛ばし_読めない行はエラーにする()
        {
            const string text = "# メモ\r\nrank,weapon,enchantment\r\n\r\nE,10,5\r\n u , 2500 , 300 \r\nC,abc,1\r\nX,1,1\r\nE,1,1\r\nD,-1,0\r\nB,,\r\n";
            var errors = new List<string>();
            List<SellPriceCsv.Row> rows = SellPriceCsv.Parse(text, errors);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(WeaponRank.E, rows[0].Rank);
            Assert.AreEqual(10, rows[0].Weapon);
            Assert.AreEqual(5, rows[0].Enchantment);
            Assert.AreEqual(WeaponRank.Unique, rows[1].Rank);
            Assert.AreEqual(2500, rows[1].Weapon);
            Assert.AreEqual(300, rows[1].Enchantment);
            Assert.AreEqual(WeaponRank.B, rows[2].Rank, "空欄は 0");
            Assert.AreEqual(0, rows[2].Weapon);
            Assert.AreEqual(4, errors.Count);
        }

        [Test]
        public void お守りの鑑定はCで1段_Aで2段付く()
        {
            string csv = File.ReadAllText("Assets/_Project/Settings/Tables/EnchantmentRanks.csv");
            var rows = EnchantmentRankCsv.Parse(csv, new List<string>()).FindAll(r => r.Kind == EnchantmentKind.Appraisal);

            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows.TrueForAll(r => r.WeaponType == "Charm"), "お守りだけに付く");
            Assert.AreEqual((WeaponRank.C, 1), (rows[0].Rank, rows[0].Level));
            Assert.AreEqual((WeaponRank.A, 2), (rows[1].Rank, rows[1].Level));
        }

        [Test]
        public void 売値の表は全ランクを読める()
        {
            var errors = new List<string>();
            List<SellPriceCsv.Row> rows = SellPriceCsv.Parse(File.ReadAllText("Assets/_Project/Settings/Tables/SellPrices.csv"), errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(7, rows.Count);
        }
    }
}
