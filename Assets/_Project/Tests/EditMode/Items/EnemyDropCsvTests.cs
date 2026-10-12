using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>敵が落とす武器の表（CSV 2 つ）の読み取りを確かめる。</summary>
    public sealed class EnemyDropCsvTests
    {
        [Test]
        public void ランクの表_見出し_空行_コメントを飛ばし_小数の確率を読む()
        {
            const string text = "# メモ\r\nenemy,E,D,C,B,A,S\r\n\r\nBlob_A,0.08,0.03, 0.005 ,,,\r\nBig_B,0.1\r\n";
            var errors = new List<string>();
            List<EnemyWeaponDropCsv.Row> rows = EnemyWeaponDropCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Blob_A", rows[0].Enemy);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(0.08f, rows[0].Chances[0], 1e-6f);
            Assert.AreEqual(0.03f, rows[0].Chances[1], 1e-6f);
            Assert.AreEqual(0.005f, rows[0].Chances[2], 1e-6f);
            Assert.AreEqual(0f, rows[0].Chances[5]);
            Assert.AreEqual(6, rows[1].Chances.Length, "列が足りなくても E〜S の 6 つ");
            Assert.AreEqual(0.1f, rows[1].Chances[0], 1e-6f);
        }

        [Test]
        public void ランクの表_読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                "Blob_A,0.08\n" +
                "Blob_B,8%\n" +
                "Blob_C,-0.1\n" +
                "Blob_D,0.6,0.5\n" +
                ",0.1\n" +
                "Blob_E,0.1,0.1,0.1,0.1,0.1,0.1,0.1\n" +
                "Blob_A,0.02\n";
            var errors = new List<string>();
            List<EnemyWeaponDropCsv.Row> rows = EnemyWeaponDropCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(6, errors.Count);
            Assert.That(errors[0], Does.StartWith("2 行目"));
            Assert.That(errors[2], Does.StartWith("4 行目").And.Contains("合計"));
            Assert.That(errors[5], Does.StartWith("7 行目").And.Contains("重複"));
        }

        [Test]
        public void ユニークの表_天井は空欄なら0()
        {
            const string text = "enemy,weapon,chance,pity\nBig_A,Weapon_X,0.005,300\nBig_A,Weapon_Y,0.01,\nBig_B,Weapon_X,0.02\n";
            var errors = new List<string>();
            List<EnemyUniqueDropCsv.Row> rows = EnemyUniqueDropCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Weapon_X", rows[0].Weapon);
            Assert.AreEqual(0.005f, rows[0].Chance, 1e-6f);
            Assert.AreEqual(300, rows[0].Pity);
            Assert.AreEqual(0, rows[1].Pity);
            Assert.AreEqual(0, rows[2].Pity, "列が 3 つでもよい");
        }

        [Test]
        public void ユニークの表_読めない行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                "Big_A,Weapon_X,0.005,300\n" +
                "Big_A,Weapon_Y\n" +
                "Big_A,Weapon_Y,1.5,1\n" +
                "Big_A,Weapon_Y,0.01,-3\n" +
                "Big_A,Weapon_Y,0,0\n" +
                "Big_A,Weapon_X,0.01,10\n" +
                "Big_A,Weapon_Z,0,10\n";
            var errors = new List<string>();
            List<EnemyUniqueDropCsv.Row> rows = EnemyUniqueDropCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count, "1 行目と、確率 0 でも天井のある 7 行目");
            Assert.AreEqual(10, rows[1].Pity);
            Assert.AreEqual(5, errors.Count);
            Assert.That(errors[3], Does.StartWith("5 行目").And.Contains("落ちることがない"));
            Assert.That(errors[4], Does.StartWith("6 行目").And.Contains("重複"));
        }
    }
}
