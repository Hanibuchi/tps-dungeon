using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>武器種ごとのエンチャントのランク表（CSV）の読み取りを確かめる。</summary>
    public sealed class EnchantmentRankCsvTests
    {
        [Test]
        public void 見出し_空行_コメントを飛ばし_ランクの列から段を読む()
        {
            const string text = "# メモ\r\nweaponType,kind,E,D,C,B,A,S\r\n\r\n01,DamageUp,1,2,3,,,\r\n 09 , homing , , , , 1 \r\n";
            var errors = new List<string>();
            List<EnchantmentRankCsv.Row> rows = EnchantmentRankCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual("01", rows[0].WeaponType);
            Assert.AreEqual(EnchantmentKind.DamageUp, rows[0].Kind);
            Assert.AreEqual(1, rows[0].Level);
            Assert.AreEqual(WeaponRank.E, rows[0].Rank);
            Assert.AreEqual(2, rows[1].Level);
            Assert.AreEqual(WeaponRank.D, rows[1].Rank);
            Assert.AreEqual(3, rows[2].Level);
            Assert.AreEqual(WeaponRank.C, rows[2].Rank);
            Assert.AreEqual(4, rows[2].Line);

            // 後ろの列を省いてもよい。
            Assert.AreEqual("09", rows[3].WeaponType);
            Assert.AreEqual(EnchantmentKind.Homing, rows[3].Kind);
            Assert.AreEqual(1, rows[3].Level);
            Assert.AreEqual(WeaponRank.B, rows[3].Rank);
        }

        [Test]
        public void 読めないセルや行は飛ばして行番号付きのエラーにする()
        {
            const string text =
                "01,DamageUp,1,,2,,,\n" +
                "01,NoSuchKind,1,,,,,\n" +
                "01,Size,0,x,1,,,\n" +
                "01,Stun,1,,1,,,\n" +
                "01,DamageUp,,,,,,1\n" +
                "01,3,1,,,,,\n" +
                "01,Pierce,1,,,,,,9\n" +
                "02,DamageUp,,,,,,1\n";
            var errors = new List<string>();
            List<EnchantmentRankCsv.Row> rows = EnchantmentRankCsv.Parse(text, errors);

            // 1 行目の 2 つ、3 行目の C の 1 段、4 行目の E の 1 段、8 行目の 1 つ。
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual(EnchantmentKind.Size, rows[2].Kind);
            Assert.AreEqual(WeaponRank.C, rows[2].Rank);
            Assert.AreEqual(EnchantmentKind.Stun, rows[3].Kind);
            Assert.AreEqual(WeaponRank.E, rows[3].Rank);
            Assert.AreEqual("02", rows[4].WeaponType);
            Assert.AreEqual(WeaponRank.S, rows[4].Rank);

            Assert.AreEqual(7, errors.Count);
            Assert.That(errors[0], Does.StartWith("2 行目"));
            Assert.That(errors[1], Does.StartWith("3 行目").And.Contains("E の列"));
            Assert.That(errors[2], Does.StartWith("3 行目").And.Contains("D の列"));
            Assert.That(errors[3], Does.StartWith("4 行目").And.Contains("2 つのランク"));
            Assert.That(errors[4], Does.StartWith("5 行目").And.Contains("重複"));
            Assert.That(errors[5], Does.StartWith("6 行目"));
            Assert.That(errors[6], Does.StartWith("7 行目").And.Contains("多すぎる"));
        }
    }
}
