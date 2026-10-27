using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>武器種ごとの値の表（WeaponTypes.csv）の読み書きを確かめる。</summary>
    public sealed class WeaponTypeCsvTests
    {
        [Test]
        public void 見出しの名前で列を引き_空行とコメントを飛ばす()
        {
            const string text =
                "# メモ\r\n" +
                "shield,memo,cooldown,id,name\r\n" +
                "\r\n" +
                " 1 ,ほげ, 0.3 ,01,片手近距離\r\n";
            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("01", rows[0].Id);
            Assert.AreEqual("片手近距離", rows[0].Name);
            Assert.AreEqual(0.3f, rows[0].Cooldown);
            Assert.IsTrue(rows[0].CanUseShield);
            Assert.AreEqual(4, rows[0].Line);
        }

        [Test]
        public void クールタイムは空なら今の値のまま_盾は1と0とtrueとfalse()
        {
            const string text =
                WeaponTypeCsv.Header + "\n" +
                "26,お守り,,0\n" +
                "09,弓,0.8,false\n" +
                "02,刺突,0,TRUE\n";
            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(3, rows.Count);
            Assert.IsNull(rows[0].Cooldown);
            Assert.IsFalse(rows[0].CanUseShield);
            Assert.AreEqual(0.8f, rows[1].Cooldown);
            Assert.IsFalse(rows[1].CanUseShield);
            Assert.AreEqual(0f, rows[2].Cooldown);
            Assert.IsTrue(rows[2].CanUseShield);
        }

        [Test]
        public void 読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                WeaponTypeCsv.Header + "\n" +
                "01,片手近距離,0.3,1\n" +
                "04,両手,-1,0\n" +
                "05,ハンマー,abc,0\n" +
                "09,弓,0.8,yes\n" +
                "11,,1.2,0\n" +
                ",雨,1.2,0\n" +
                "01,片手近距離,0.5,0\n" +
                "19,\"閉じていない,0.9,0\n" +
                "20,石棘,1,0\n";
            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count, "2 行目と最後の行だけ");
            Assert.AreEqual("01", rows[0].Id);
            Assert.AreEqual(0.3f, rows[0].Cooldown, "重複した後の行で上書きしない");
            Assert.AreEqual("20", rows[1].Id);
            Assert.AreEqual(7, errors.Count);
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("9 行目", errors[6]);
        }

        [Test]
        public void 見出しに要る列が無ければ読まない()
        {
            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse("id,name,cooldown\n01,片手近距離,0.3\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("shield", errors[0]);
        }

        [Test]
        public void 書き出した表を読み直すと同じになる()
        {
            var written = new List<WeaponTypeCsv.Row>
            {
                new WeaponTypeCsv.Row(0, "01", "片手,近距離", 0.3f, true),
                new WeaponTypeCsv.Row(0, "26", "お守り", null, false),
            };

            string text = WeaponTypeCsv.Format(written);
            var errors = new List<string>();
            List<WeaponTypeCsv.Row> rows = WeaponTypeCsv.Parse(text, errors);

            StringAssert.StartsWith(WeaponTypeCsv.Header + "\n", text);
            StringAssert.Contains("26,お守り,,0\n", text);
            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(written[i].Id, rows[i].Id);
                Assert.AreEqual(written[i].Name, rows[i].Name);
                Assert.AreEqual(written[i].Cooldown, rows[i].Cooldown);
                Assert.AreEqual(written[i].CanUseShield, rows[i].CanUseShield);
            }
        }
    }
}
