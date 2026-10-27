using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>武器ごとの値の表（Weapons.csv）の読み書きを確かめる。</summary>
    public sealed class WeaponCsvTests
    {
        [Test]
        public void 見出しの名前で列を引き_空行とコメントを飛ばす()
        {
            const string text =
                "# メモ\r\n" +
                "name,strength,id,memo,rank,description\r\n" +
                "\r\n" +
                "鉄の剣, 21 ,Weapon_A,ほげ,c,ありふれた剣。\r\n";
            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Weapon_A", rows[0].Id);
            Assert.AreEqual(WeaponRank.C, rows[0].Rank);
            Assert.AreEqual(21f, rows[0].Strength);
            Assert.AreEqual("鉄の剣", rows[0].Name);
            Assert.AreEqual("ありふれた剣。", rows[0].Description);
            Assert.AreEqual(4, rows[0].Line);
        }

        [Test]
        public void 囲んだ欄は中のカンマと二重の引用符と改行を読む()
        {
            const string text =
                WeaponCsv.Header + "\n" +
                "Weapon_A,U,8.5,\"影,駆け\",\"「\"\"速い\"\"」\\n二行目\"\n";
            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(WeaponRank.Unique, rows[0].Rank);
            Assert.AreEqual(8.5f, rows[0].Strength);
            Assert.AreEqual("影,駆け", rows[0].Name);
            Assert.AreEqual("「\"速い\"」\n二行目", rows[0].Description);
        }

        [Test]
        public void 説明は空でよい()
        {
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(WeaponCsv.Header + "\nWeapon_A,E,3,素手,\n", new List<string>());

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("", rows[0].Description);
        }

        [TestCase("U", WeaponRank.Unique)]
        [TestCase("unique", WeaponRank.Unique)]
        [TestCase("s", WeaponRank.S)]
        [TestCase(" E ", WeaponRank.E)]
        public void ランクの表記を読む(string cell, WeaponRank expected)
        {
            Assert.IsTrue(WeaponCsv.TryParseRank(cell, out WeaponRank rank));
            Assert.AreEqual(expected, rank);
        }

        [Test]
        public void 読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                WeaponCsv.Header + "\n" +
                "Weapon_A,E,3,剣,\n" +
                "Weapon_B,X,3,剣,\n" +
                "Weapon_C,E,-1,剣,\n" +
                "Weapon_D,E,abc,剣,\n" +
                "Weapon_E,E,3,,\n" +
                ",E,3,剣,\n" +
                "Weapon_A,D,4,剣,\n" +
                "Weapon_F,E,3,\"閉じていない,\n" +
                "Weapon_G,E,3,剣,\n";
            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count, "2 行目と最後の行だけ");
            Assert.AreEqual("Weapon_A", rows[0].Id);
            Assert.AreEqual(3f, rows[0].Strength, "重複した後の行で上書きしない");
            Assert.AreEqual("Weapon_G", rows[1].Id);
            Assert.AreEqual(7, errors.Count);
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("9 行目", errors[6]);
        }

        [Test]
        public void 見出しに要る列が無ければ読まない()
        {
            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse("id,rank,name\nWeapon_A,E,剣\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("strength", errors[0]);
        }

        [Test]
        public void 書き出した表を読み直すと同じになる()
        {
            var written = new List<WeaponCsv.Row>
            {
                new WeaponCsv.Row(0, "Weapon_A", WeaponRank.Unique, 8.25f, "影,駆け", "「\"速い\"」\n二行目"),
                new WeaponCsv.Row(0, "Weapon_B", WeaponRank.E, 3f, "素手", ""),
            };

            string text = WeaponCsv.Format(written);
            var errors = new List<string>();
            List<WeaponCsv.Row> rows = WeaponCsv.Parse(text, errors);

            StringAssert.StartsWith(WeaponCsv.Header + "\n", text);
            StringAssert.Contains("Weapon_B,E,3,素手,\n", text);
            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(written[i].Id, rows[i].Id);
                Assert.AreEqual(written[i].Rank, rows[i].Rank);
                Assert.AreEqual(written[i].Strength, rows[i].Strength);
                Assert.AreEqual(written[i].Name, rows[i].Name);
                Assert.AreEqual(written[i].Description, rows[i].Description);
            }
        }
    }
}
