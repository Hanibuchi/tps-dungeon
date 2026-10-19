using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの、箱から出る武器のランクの重み（BreakableWeaponRanks.csv）の読み取りを確かめる。</summary>
    public sealed class BreakableWeaponRankCsvTests
    {
        [Test]
        public void 見出しの名前で列を引き_空は0()
        {
            const string text =
                "﻿# メモ\r\n" +
                "S,A,B,C,D,E,layer,memo\r\n" +
                "\r\n" +
                ",,,0.5,3,10,1,浅い\r\n" +
                " 0.2 ,1,3,6,4,2, 5 \r\n";
            var errors = new List<string>();
            List<BreakableWeaponRankCsv.Row> rows = BreakableWeaponRankCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].Layer);
            Assert.AreEqual(4, rows[0].Line);
            CollectionAssert.AreEqual(new[] { 10f, 3f, 0.5f, 0f, 0f, 0f }, rows[0].Weights);
            Assert.AreEqual(5, rows[1].Layer);
            CollectionAssert.AreEqual(new[] { 2f, 4f, 6f, 3f, 1f, 0.2f }, rows[1].Weights);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "layer,E,D,C,B,A,S\n" +
                "1,10,3,,,,\n" +
                "0,1,,,,,\n" +       // 層 0
                "2,-1,1,,,,\n" +     // 負の重み
                "2,x,1,,,,\n" +      // 数でない
                "2,,,,,,\n" +        // 全部 0
                "1,5,,,,,\n";        // 重複
            var errors = new List<string>();
            List<BreakableWeaponRankCsv.Row> rows = BreakableWeaponRankCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.Contains("全部 0", errors[3]);
            StringAssert.Contains("重複", errors[4]);
        }

        [Test]
        public void ランクの列が足りなければ読まない()
        {
            var errors = new List<string>();
            List<BreakableWeaponRankCsv.Row> rows = BreakableWeaponRankCsv.Parse("layer,E,D\n1,1,1\n", errors);
            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("C", errors[0]);
        }
    }
}
