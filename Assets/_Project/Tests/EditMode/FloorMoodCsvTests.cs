using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの階の光と色（FloorMoods.csv）の読み取りを確かめる。</summary>
    public sealed class FloorMoodCsvTests
    {
        private const string Header = "layer,ambient,filter,saturation,vignette,torch_color,torch_intensity,torch_range,flicker";

        [Test]
        public void 色と数を読み_見出しの並びは自由()
        {
            const string text =
                "﻿# メモ\r\n" +
                "flicker,torch_range,torch_intensity,torch_color,vignette,saturation,filter,ambient,layer,memo\r\n" +
                "\r\n" +
                "0.25,7,2.5,#FF7030,0.38,-25,B6B0D2,#5C5A7C,5,暗い\r\n";
            var errors = new List<string>();
            List<FloorMoodCsv.Row> rows = FloorMoodCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(1, rows.Count);
            FloorMoodCsv.Row row = rows[0];
            Assert.AreEqual(5, row.Layer);
            Assert.AreEqual(4, row.Line);
            Assert.AreEqual(0x5C / 255f, row.Ambient.R, 1e-5f);
            Assert.AreEqual(0x7C / 255f, row.Ambient.B, 1e-5f);
            Assert.AreEqual(0xB6 / 255f, row.Filter.R, 1e-5f, "# は省いてもよい");
            Assert.AreEqual(-25f, row.Saturation);
            Assert.AreEqual(0.38f, row.Vignette);
            Assert.AreEqual(1f, row.TorchColor.R, 1e-5f);
            Assert.AreEqual(2.5f, row.TorchIntensity);
            Assert.AreEqual(7f, row.TorchRange);
            Assert.AreEqual(0.25f, row.Flicker);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            string text =
                Header + "\n" +
                "1,#B8AE9C,#FFFFFF,0,0.15,#FF9A4D,2.2,8,0.12\n" +
                "2,#B8AE9,#FFFFFF,0,0.15,#FF9A4D,2.2,8,0.12\n" +    // 色が 5 桁
                "2,#B8AE9C,#FFFFFF,-120,0.15,#FF9A4D,2.2,8,0.12\n" + // 彩度が範囲の外
                "2,#B8AE9C,#FFFFFF,0,1.5,#FF9A4D,2.2,8,0.12\n" +     // 周りの暗さが 1 を超える
                "2,#B8AE9C,#FFFFFF,0,0.15,#FF9A4D,2.2,0,0.12\n" +    // 距離 0
                "2,#B8AE9C,#FFFFFF,0,0.15,#FF9A4D,2.2,8,\n" +        // 空
                "1,#B8AE9C,#FFFFFF,0,0.15,#FF9A4D,2.2,8,0.12\n";     // 重複
            var errors = new List<string>();
            List<FloorMoodCsv.Row> rows = FloorMoodCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(6, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("ambient", errors[0]);
            StringAssert.Contains("saturation", errors[1]);
            StringAssert.Contains("vignette", errors[2]);
            StringAssert.Contains("torch_range", errors[3]);
            StringAssert.Contains("flicker", errors[4]);
            StringAssert.Contains("重複", errors[5]);
        }

        [Test]
        public void 見出しが足りなければ読まない()
        {
            var errors = new List<string>();
            List<FloorMoodCsv.Row> rows = FloorMoodCsv.Parse("layer,ambient\n1,#FFFFFF\n", errors);
            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("torch_color", errors[0]);
        }

        [Test]
        public void 色の書き方()
        {
            Assert.IsTrue(FloorMoodCsv.TryParseColor("#FF8000", out FloorMoodCsv.Rgb c));
            Assert.AreEqual(1f, c.R, 1e-5f);
            Assert.AreEqual(128f / 255f, c.G, 1e-5f);
            Assert.AreEqual(0f, c.B, 1e-5f);
            Assert.IsFalse(FloorMoodCsv.TryParseColor("", out _));
            Assert.IsFalse(FloorMoodCsv.TryParseColor("#GG0000", out _));
            Assert.IsFalse(FloorMoodCsv.TryParseColor("#FFF", out _));
        }
    }
}
