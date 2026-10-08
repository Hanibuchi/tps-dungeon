using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>ユニーク武器の固定エンチャントの表（CSV）の読み取りを確かめる。</summary>
    public sealed class UniqueEnchantmentCsvTests
    {
        [Test]
        public void 見出し_空行_コメントを飛ばして行を読む()
        {
            const string text = "# メモ\r\nweapon,kind,level\r\n\r\nWeapon_A,CritChance,3\r\n Weapon_A , multishot , 1 \r\n";
            var errors = new List<string>();
            List<UniqueEnchantmentCsv.Row> rows = UniqueEnchantmentCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Weapon_A", rows[0].Weapon);
            Assert.AreEqual(EnchantmentKind.CritChance, rows[0].Kind);
            Assert.AreEqual(3, rows[0].Level);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(EnchantmentKind.Multishot, rows[1].Kind);
            Assert.AreEqual(1, rows[1].Level);
        }

        [Test]
        public void 読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                "Weapon_A,CritChance,3\n" +
                "Weapon_A,NoSuchKind,1\n" +
                "Weapon_A,Size,0\n" +
                "Weapon_A,Size\n" +
                ",Size,1\n" +
                "Weapon_A,CritChance,1\n" +
                "Weapon_B,CritChance,1\n";
            var errors = new List<string>();
            List<UniqueEnchantmentCsv.Row> rows = UniqueEnchantmentCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count, "1 行目と、別の武器の 7 行目だけ");
            Assert.AreEqual("Weapon_B", rows[1].Weapon);
            Assert.AreEqual(5, errors.Count);
            Assert.That(errors[0], Does.StartWith("2 行目"));
            Assert.That(errors[4], Does.StartWith("6 行目").And.Contains("重複"));
        }
    }
}
