using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Items.Tests
{
    /// <summary>武器の情報欄の本文の書式。</summary>
    public sealed class ItemDetailTextTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void 武器はランクと攻撃力と説明を出し_武器種やDPSは出さない()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var type = ScriptableObject.CreateInstance<WeaponTypeDefinition>();
            try
            {
                typeof(WeaponDefinition).GetField("rank", Private).SetValue(weapon, WeaponRank.C);
                typeof(WeaponDefinition).GetField("strength", Private).SetValue(weapon, 21f);
                typeof(WeaponDefinition).GetField("weaponType", Private).SetValue(weapon, type);
                typeof(ItemDefinition).GetField("description", Private).SetValue(weapon, "ありふれた剣。");
                typeof(WeaponTypeDefinition).GetField("displayName", Private).SetValue(type, "片手近距離");

                string text = new ItemInstance(weapon).DetailText();
                string[] lines = text.Split('\n');

                StringAssert.StartsWith("ランク ", lines[0]);
                StringAssert.Contains("C", lines[0]);
                Assert.AreEqual("攻撃力 21", lines[1]);
                Assert.AreEqual("", lines[2], "説明の前は 1 行空ける");
                Assert.AreEqual("ありふれた剣。", lines[3]);
                StringAssert.DoesNotContain("片手近距離", text);
                StringAssert.DoesNotContain("DPS", text);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(type);
            }
        }

        [Test]
        public void 武器でなければ説明だけ()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                typeof(ItemDefinition).GetField("description", Private).SetValue(item, "赤く澄んだ薬。");
                Assert.AreEqual("赤く澄んだ薬。", new ItemInstance(item).DetailText());
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void 盾は攻撃力の代わりに防御力と効き方を出し_お守りは強さを出さない()
        {
            var shield = ScriptableObject.CreateInstance<WeaponDefinition>();
            var charm = ScriptableObject.CreateInstance<WeaponDefinition>();
            var shieldType = ScriptableObject.CreateInstance<WeaponTypeDefinition>();
            var charmType = ScriptableObject.CreateInstance<WeaponTypeDefinition>();
            try
            {
                typeof(WeaponTypeDefinition).GetField("passiveGear", Private).SetValue(shieldType, PassiveGear.Shield);
                typeof(WeaponTypeDefinition).GetField("passiveGear", Private).SetValue(charmType, PassiveGear.Charm);
                typeof(WeaponDefinition).GetField("strength", Private).SetValue(shield, 18f);
                typeof(WeaponDefinition).GetField("weaponType", Private).SetValue(shield, shieldType);
                typeof(WeaponDefinition).GetField("strength", Private).SetValue(charm, 14f);
                typeof(WeaponDefinition).GetField("weaponType", Private).SetValue(charm, charmType);

                string shieldText = new ItemInstance(shield).DetailText();
                Assert.AreEqual("防御力 18", shieldText.Split('\n')[1]);
                StringAssert.DoesNotContain("攻撃力", shieldText);
                StringAssert.Contains("片手武器を持っている間だけ効く", shieldText);

                string charmText = new ItemInstance(charm).DetailText();
                StringAssert.DoesNotContain("攻撃力", charmText);
                StringAssert.DoesNotContain("14", charmText);
                StringAssert.Contains("ホットバーに入れておくだけで効く", charmText);
            }
            finally
            {
                Object.DestroyImmediate(shield);
                Object.DestroyImmediate(charm);
                Object.DestroyImmediate(shieldType);
                Object.DestroyImmediate(charmType);
            }
        }
    }
}
