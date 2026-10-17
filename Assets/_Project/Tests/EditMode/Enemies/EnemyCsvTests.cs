using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>敵の台帳（Enemies.csv）の読み取りを確かめる。</summary>
    public sealed class EnemyCsvTests
    {
        [Test]
        public void 見出し_空行_コメントを飛ばし_id_コスト_層を読む()
        {
            const string text = "﻿# メモ\r\nid,cost,layers\r\n\r\npink_blob,1,1-5\r\n orc , 4.5 , 2-3;5 \r\n";
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("pink_blob", rows[0].Id);
            Assert.AreEqual(1f, rows[0].Cost);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rows[0].Layers);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual("orc", rows[1].Id);
            Assert.AreEqual(4.5f, rows[1].Cost);
            CollectionAssert.AreEqual(new[] { 2, 3, 5 }, rows[1].Layers);
        }

        [Test]
        public void 列は見出しの名前で引き_並びが違っても知らない列があっても読める()
        {
            const string text = "layers,hp,id,attack,cost\n3;1,60,orc,12,4\n";
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("orc", rows[0].Id);
            Assert.AreEqual(4f, rows[0].Cost);
            CollectionAssert.AreEqual(new[] { 1, 3 }, rows[0].Layers);
        }

        [Test]
        public void 層が空の敵は部屋に出ず_コストも空でよい()
        {
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse("id,cost,layers\nboss_orc,,\n", errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(1, rows.Count);
            Assert.IsEmpty(rows[0].Layers);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "id,cost,layers\n" +
                "ok,1,1\n" +
                ",1,1\n" +              // id が空
                "Pink_Blob,1,1\n" +     // 大文字
                "a,0,1\n" +             // 出るのにコスト 0
                "b,-1,\n" +             // 負のコスト
                "c,1,0\n" +             // 層 0
                "d,1,3-1\n" +           // 逆向きの範囲
                "e,1,1,2\n" +           // , で区切った（2 は知らない列）→ 層 1 だけ
                "f,1,x\n" +             // 数でない層
                "ok,2,2\n";             // 重複
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("ok", rows[0].Id);
            Assert.AreEqual("e", rows[1].Id);
            Assert.AreEqual(8, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("11 行目", errors[7]);
        }

        [Test]
        public void 見出しに要る列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse("id,cost\npink_blob,1\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("layers", errors[0]);
        }

        [TestCase("", new int[0])]
        [TestCase("2", new[] { 2 })]
        [TestCase("1-3", new[] { 1, 2, 3 })]
        [TestCase("5;1-2;2", new[] { 1, 2, 5 })]
        [TestCase(" 1 - 2 ; 4 ", new[] { 1, 2, 4 })]
        public void 層の書き方(string text, int[] expected)
        {
            Assert.IsTrue(EnemyCsv.TryParseLayers(text, out int[] layers, out string error), error);
            CollectionAssert.AreEqual(expected, layers);
        }
    }
}
