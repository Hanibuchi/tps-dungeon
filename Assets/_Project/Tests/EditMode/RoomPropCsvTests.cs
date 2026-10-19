using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの部屋の置き物の顔ぶれ（RoomProps.csv）の読み取りを確かめる。</summary>
    public sealed class RoomPropCsvTests
    {
        [Test]
        public void 見出しの名前で列を引いて重みと机の確率を読む()
        {
            const string text =
                "# メモ\r\n" +
                "table_chance,candles_unlit,candles,potions,books,bones,layer\r\n" +
                "0.15,2,0.5,0.5,1,5,5\r\n";
            var errors = new List<string>();
            List<RoomPropCsv.Row> rows = RoomPropCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(5, rows[0].Layer);
            Assert.AreEqual(5f, rows[0].Bones);
            Assert.AreEqual(1f, rows[0].Books);
            Assert.AreEqual(0.5f, rows[0].Potions);
            Assert.AreEqual(0.5f, rows[0].Candles);
            Assert.AreEqual(2f, rows[0].CandlesUnlit);
            Assert.AreEqual(0.15f, rows[0].TableChance);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "layer,bones,books,potions,candles,candles_unlit,table_chance\n" +
                "1,1,3,2,2,0,0.5\n" +
                "2,-1,3,2,2,0,0.5\n" +   // 負の重み
                "2,1,3,2,2,0,1.5\n" +    // 確率が 1 を超える
                "2,1,,2,2,0,0.5\n" +     // 空
                "1,1,3,2,2,0,0.5\n";     // 重複
            var errors = new List<string>();
            List<RoomPropCsv.Row> rows = RoomPropCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(4, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("bones", errors[0]);
            StringAssert.Contains("table_chance", errors[1]);
            StringAssert.Contains("books", errors[2]);
            StringAssert.Contains("重複", errors[3]);
        }
    }
}
