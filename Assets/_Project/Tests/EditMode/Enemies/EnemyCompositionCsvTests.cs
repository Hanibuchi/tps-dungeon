using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>部屋の敵の編成タイプ（EnemyCompositions.csv）の読み取りを確かめる。</summary>
    public sealed class EnemyCompositionCsvTests
    {
        [Test]
        public void 見出し_空行_コメントを飛ばし_比率と全部の印を読む()
        {
            const string text =
                "﻿# メモ\r\n" +
                "name,layers,weight,shares\r\n" +
                "\r\n" +
                "混成,1-5,2,*\r\n" +
                " オーク隊 , 2-3 , 1 , orc:0.6 ; green_blob : 0.4 \r\n" +
                "オーク多め,4,0.5,orc;green_blob:0.25\r\n";
            var errors = new List<string>();
            List<EnemyCompositionCsv.Row> rows = EnemyCompositionCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(3, rows.Count);

            Assert.AreEqual("混成", rows[0].Name);
            Assert.AreEqual(4, rows[0].Line);
            Assert.IsTrue(rows[0].AllEnemies);
            Assert.IsEmpty(rows[0].Shares);
            Assert.AreEqual(2f, rows[0].Weight);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rows[0].Layers);

            Assert.AreEqual("オーク隊", rows[1].Name);
            Assert.IsFalse(rows[1].AllEnemies);
            CollectionAssert.AreEqual(new[] { 2, 3 }, rows[1].Layers);
            Assert.AreEqual(2, rows[1].Shares.Length);
            Assert.AreEqual("orc", rows[1].Shares[0].EnemyId);
            Assert.AreEqual(0.6f, rows[1].Shares[0].Value);
            Assert.AreEqual("green_blob", rows[1].Shares[1].EnemyId);
            Assert.AreEqual(0.4f, rows[1].Shares[1].Value);

            // 比率を省くと 1。
            Assert.AreEqual(1f, rows[2].Shares[0].Value);
            Assert.AreEqual(0.25f, rows[2].Shares[1].Value);
        }

        [Test]
        public void 列は見出しの名前で引き_並びが違っても知らない列があっても読める()
        {
            const string text = "shares,memo,weight,name,layers\norc:2;ghost,メモ,1.5,空飛ぶ隊,1;3\n";
            var errors = new List<string>();
            List<EnemyCompositionCsv.Row> rows = EnemyCompositionCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("空飛ぶ隊", rows[0].Name);
            Assert.AreEqual(1.5f, rows[0].Weight);
            CollectionAssert.AreEqual(new[] { 1, 3 }, rows[0].Layers);
            Assert.AreEqual("orc", rows[0].Shares[0].EnemyId);
            Assert.AreEqual(2f, rows[0].Shares[0].Value);
            Assert.AreEqual("ghost", rows[0].Shares[1].EnemyId);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "name,layers,weight,shares\n" +
                "ok,1,1,orc\n" +
                ",1,1,orc\n" +              // name が空
                "a,0,1,orc\n" +             // 層 0
                "b,1,-1,orc\n" +            // 負の重み
                "c,1,,orc\n" +              // 重みが空
                "d,1,1,\n" +                // shares が空
                "e,1,1,orc:x\n" +           // 比率が数でない
                "f,1,1,orc:-1\n" +          // 負の比率
                "g,1,1,orc;orc:2\n" +       // 同じ id が 2 回
                "h,1,1,*;orc\n" +           // * とほかの id
                "i,1,1,Orc\n" +             // 大文字
                "ok,2,0,ghost\n";           // 名前は重なってもよく、重み 0 もよい
            var errors = new List<string>();
            List<EnemyCompositionCsv.Row> rows = EnemyCompositionCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(0f, rows[1].Weight);
            Assert.AreEqual(10, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("12 行目", errors[9]);
        }

        [Test]
        public void 見出しに要る列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<EnemyCompositionCsv.Row> rows = EnemyCompositionCsv.Parse("name,layers,weight\n混成,1,1\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("shares", errors[0]);
        }
    }
}
