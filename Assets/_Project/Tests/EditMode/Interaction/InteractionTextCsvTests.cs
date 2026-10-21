using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Interaction.Tests
{
    /// <summary>インタラクトできる場所の文字（InteractionTexts.csv）の読み取りを確かめる。</summary>
    public sealed class InteractionTextCsvTests
    {
        private const string Header = "id,prompt,closed_prompt,title,description";

        [Test]
        public void 文字を読み_見出しの並びは自由()
        {
            const string text =
                "﻿# メモ\r\n" +
                "description,title,memo,closed_prompt,prompt,id\r\n" +
                "\r\n" +
                "持ち物を預けられる。,銀行,あとで,準備中,入る,bank\r\n";
            var errors = new List<string>();
            List<InteractionTextCsv.Row> rows = InteractionTextCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(1, rows.Count);
            InteractionTextCsv.Row row = rows[0];
            Assert.AreEqual(4, row.Line);
            Assert.AreEqual("bank", row.Id);
            Assert.AreEqual("入る", row.Prompt);
            Assert.AreEqual("準備中", row.ClosedPrompt);
            Assert.AreEqual("銀行", row.Title);
            Assert.AreEqual("持ち物を預けられる。", row.Description);
        }

        [Test]
        public void 囲んだ欄の中のカンマと改行と空の欄を読む()
        {
            string text =
                Header + "\n" +
                "rebirth,転生する,,転生の渦,\"ここに入ると, 転生する\\n城の前で目覚める\"\n" +
                "quote,調べる,,\"\"\"渦\"\"\",本文\n";
            var errors = new List<string>();
            List<InteractionTextCsv.Row> rows = InteractionTextCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("", rows[0].ClosedPrompt, "空なら閉じている間は触れない");
            Assert.AreEqual("ここに入ると, 転生する\n城の前で目覚める", rows[0].Description);
            Assert.AreEqual("\"渦\"", rows[1].Title);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            string text =
                Header + "\n" +
                "bank,入る,準備中,銀行,本文\n" +
                ",入る,準備中,銀行,本文\n" +        // id が空
                "bank,入る,準備中,銀行,本文\n" +    // 重複
                "shop,入る,準備中,\"武器屋,本文\n";  // 囲みが閉じていない
            var errors = new List<string>();
            List<InteractionTextCsv.Row> rows = InteractionTextCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("4 行目", errors[1]);
            StringAssert.StartsWith("5 行目", errors[2]);
        }

        [Test]
        public void 見出しが足りなければ何も読まない()
        {
            var errors = new List<string>();
            List<InteractionTextCsv.Row> rows = InteractionTextCsv.Parse("id,prompt,title\nbank,入る,銀行\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("closed_prompt", errors[0]);
            StringAssert.Contains("description", errors[0]);
        }
    }
}
