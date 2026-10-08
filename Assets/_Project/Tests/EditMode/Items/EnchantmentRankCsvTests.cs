using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>武器種ごとのエンチャントのランク表（CSV）の読み取りを確かめる。</summary>
    public sealed class EnchantmentRankCsvTests
    {
        [Test]
        public void 見出し_空行_コメントを飛ばして行を読む()
        {
            const string text = "# メモ\r\nweaponType,kind,level,rank\r\n\r\n01,DamageUp,1,E\r\n 01 , damageup , 2 , c \r\n09,Homing,1,B\r\n";
            var errors = new List<string>();
            List<EnchantmentRankCsv.Row> rows = EnchantmentRankCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("01", rows[0].WeaponType);
            Assert.AreEqual(EnchantmentKind.DamageUp, rows[0].Kind);
            Assert.AreEqual(1, rows[0].Level);
            Assert.AreEqual(WeaponRank.E, rows[0].Rank);
            Assert.AreEqual(2, rows[1].Level);
            Assert.AreEqual(WeaponRank.C, rows[1].Rank);
            Assert.AreEqual(5, rows[1].Line);
            Assert.AreEqual(EnchantmentKind.Homing, rows[2].Kind);
        }

        [Test]
        public void 読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                "01,DamageUp,1,E\n" +
                "01,NoSuchKind,1,E\n" +
                "01,Size,0,E\n" +
                "01,Size,x,E\n" +
                "01,Size,1,U\n" +
                "01,Size,1\n" +
                "01,DamageUp,1,C\n" +
                "01,3,1,E\n" +
                "02,DamageUp,1,C\n";
            var errors = new List<string>();
            List<EnchantmentRankCsv.Row> rows = EnchantmentRankCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count, "1 行目と、別の武器種の 9 行目だけ");
            Assert.AreEqual("02", rows[1].WeaponType);
            Assert.AreEqual(7, errors.Count);
            Assert.That(errors[0], Does.StartWith("2 行目"));
            Assert.That(errors[5], Does.StartWith("7 行目").And.Contains("重複"));
        }
    }
}
