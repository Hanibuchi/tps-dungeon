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
    }
}
