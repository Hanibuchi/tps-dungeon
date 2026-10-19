using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>箱を壊したときの抽選（敵・武器・何も出ない、と重みでの選び方）と、その表（Breakables.csv）の読み取り。</summary>
    public sealed class BreakableDropTests
    {
        [Test]
        public void 重みを合計で割って選ぶ()
        {
            var weights = new[] { 1f, 3f };
            Assert.AreEqual(0, BreakableDropRoller.PickWeighted(weights, 0d));
            Assert.AreEqual(0, BreakableDropRoller.PickWeighted(weights, 0.249d));
            Assert.AreEqual(1, BreakableDropRoller.PickWeighted(weights, 0.25d), "ちょうど境目は次");
            Assert.AreEqual(1, BreakableDropRoller.PickWeighted(weights, 0.9999999d));
        }

        [Test]
        public void 重み0と負は選ばず_全部0なら選べない()
        {
            Assert.AreEqual(2, BreakableDropRoller.PickWeighted(new[] { 0f, -1f, 2f }, 0d));
            Assert.AreEqual(2, BreakableDropRoller.PickWeighted(new[] { 0f, -1f, 2f }, 0.99d));
            Assert.AreEqual(-1, BreakableDropRoller.PickWeighted(new[] { 0f, -1f }, 0.5d));
            Assert.AreEqual(-1, BreakableDropRoller.PickWeighted(new float[0], 0.5d));
        }

        [Test]
        public void 敵_武器_何も出ないの順に割り振る()
        {
            // 敵 0.2・武器 0.3・何も出ない 0.5。
            Assert.AreEqual(BreakableOutcome.Enemy, BreakableDropRoller.RollOutcome(0.2f, 0.3f, 0.5f, 0.1d));
            Assert.AreEqual(BreakableOutcome.Weapon, BreakableDropRoller.RollOutcome(0.2f, 0.3f, 0.5f, 0.2d));
            Assert.AreEqual(BreakableOutcome.Weapon, BreakableDropRoller.RollOutcome(0.2f, 0.3f, 0.5f, 0.49d));
            Assert.AreEqual(BreakableOutcome.Nothing, BreakableDropRoller.RollOutcome(0.2f, 0.3f, 0.5f, 0.51d));
            // 合計が 1 でなくても比で。
            Assert.AreEqual(BreakableOutcome.Enemy, BreakableDropRoller.RollOutcome(2f, 0f, 2f, 0.49d));
            Assert.AreEqual(BreakableOutcome.Nothing, BreakableDropRoller.RollOutcome(2f, 0f, 2f, 0.51d));
            Assert.AreEqual(BreakableOutcome.Nothing, BreakableDropRoller.RollOutcome(0f, 0f, 0f, 0.1d));
        }

        [Test]
        public void 表を見出しの名前で読み_知らない列は飛ばす()
        {
            const string text =
                "﻿# 箱の中身\r\n" +
                "kind,memo,nothing,weapon,enemy\r\n" +
                "\r\n" +
                "crate,木箱,0.7,0.05,0.25\r\n" +
                " Chest ,, 0.15 , 0.5 , 0.35 \r\n";
            var errors = new List<string>();
            List<BreakableDropCsv.Row> rows = BreakableDropCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(BreakableDropKind.Crate, rows[0].Kind);
            Assert.AreEqual(0.25f, rows[0].Enemy);
            Assert.AreEqual(0.05f, rows[0].Weapon);
            Assert.AreEqual(0.7f, rows[0].Nothing);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(BreakableDropKind.Chest, rows[1].Kind);
            Assert.AreEqual(0.35f, rows[1].Enemy);
            Assert.AreEqual(0.5f, rows[1].Weapon);
            Assert.AreEqual(0.15f, rows[1].Nothing);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "kind,enemy,weapon,nothing\n" +
                "crate,0.2,0,0.8\n" +
                "barrel,0.2,0,0.8\n" +    // 知らない種類
                "chest,-1,0,1\n" +        // 負の重み
                "chest,0,0,0\n" +         // 全部 0
                "chest,,1,0\n" +          // 空
                "chest,0,1,0\n" +         // 武器だけ出る（読める）
                "crate,0.2,0,0.8\n";      // 重複
            var errors = new List<string>();
            List<BreakableDropCsv.Row> rows = BreakableDropCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.Contains("重複", errors[4]);
        }

        [Test]
        public void 見出しが足りなければ読まない()
        {
            var errors = new List<string>();
            List<BreakableDropCsv.Row> rows = BreakableDropCsv.Parse("kind,enemy\ncrate,1\n", errors);
            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("weapon", errors[0]);
        }
    }
}
