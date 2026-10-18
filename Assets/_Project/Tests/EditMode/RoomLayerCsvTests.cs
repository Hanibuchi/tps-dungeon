using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの普通の部屋（Rooms.csv）の読み取りを確かめる。</summary>
    public sealed class RoomLayerCsvTests
    {
        [Test]
        public void 見出しの名前で列を引いて部屋ごとの値を読む()
        {
            const string text =
                "# メモ\r\n" +
                "weight,note,size_y,layer,size_x\r\n" +
                "\r\n" +
                "0.8,細長い,2,1,1\r\n" +
                " 0 , , 3 , 2 , 2 \r\n";
            var errors = new List<string>();
            List<RoomLayerCsv.Row> rows = RoomLayerCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].Layer);
            Assert.AreEqual(1, rows[0].Width);
            Assert.AreEqual(2, rows[0].Height);
            Assert.AreEqual(0.8f, rows[0].Weight);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(2, rows[1].Layer);
            Assert.AreEqual(2, rows[1].Width);
            Assert.AreEqual(3, rows[1].Height);
            Assert.AreEqual(0f, rows[1].Weight);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "layer,size_x,size_y,weight\n" +
                "1,1,1,1\n" +
                "0,1,1,1\n" +       // 層 0
                "1.5,1,1,1\n" +     // 整数でない層
                "1,0,1,1\n" +       // 幅 0
                "1,1,-2,1\n" +      // 負の奥行き
                "1,2,2,-1\n" +      // 負の重み
                "1,2,2,\n" +        // 重みが空
                "1,1,1,0.5\n" +     // 重複
                "2,1,1,1\n";        // 層が違えば同じ大きさでもよい
            var errors = new List<string>();
            List<RoomLayerCsv.Row> rows = RoomLayerCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(7, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.Contains("重複", errors[6]);
        }

        [Test]
        public void 見出しに要る列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<RoomLayerCsv.Row> rows = RoomLayerCsv.Parse("layer,size_x\n1,1\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("size_y", errors[0]);
            StringAssert.Contains("weight", errors[0]);
        }
    }
}
