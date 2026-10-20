using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの階の広さ（Floors.csv）の読み取りと、部屋数への直し方を確かめる。</summary>
    public sealed class FloorLayerCsvTests
    {
        [Test]
        public void 見出しの名前で列を引いて層ごとの面積を読む()
        {
            const string text =
                "# メモ\r\n" +
                "note,area,layer\r\n" +
                "\r\n" +
                "狭い,27,1\r\n" +
                " , 35.5 , 2 \r\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].Layer);
            Assert.AreEqual(27f, rows[0].Area);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(2, rows[1].Layer);
            Assert.AreEqual(35.5f, rows[1].Area);
        }

        [Test]
        public void 木箱の数と宝箱の確率は列が無いか空なら変えない()
        {
            const string text =
                "layer,area,crates_per_cell,chest_chance\n" +
                "1,20,0.4,0.15\n" +
                "2,30,,\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.IsTrue(rows[0].HasCratesPerCell);
            Assert.AreEqual(0.4f, rows[0].CratesPerCell);
            Assert.AreEqual(0.15f, rows[0].ChestChance);
            Assert.IsFalse(rows[1].HasCratesPerCell);
            Assert.IsFalse(rows[1].HasChestChance);

            List<FloorLayerCsv.Row> old = FloorLayerCsv.Parse("layer,area\n1,20\n", errors);
            Assert.IsFalse(old[0].HasCratesPerCell);
            Assert.IsFalse(old[0].HasChestChance);
        }

        [Test]
        public void 壁の高さは列が無いか空なら変えず_2未満なら読まない()
        {
            const string text =
                "layer,area,wall_height\n" +
                "1,20,6.5\n" +
                "2,30,\n" +
                "3,40,1.5\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows[0].HasWallHeight);
            Assert.AreEqual(6.5f, rows[0].WallHeight);
            Assert.IsFalse(rows[1].HasWallHeight);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("wall_height", errors[0]);
        }

        [Test]
        public void 木箱の数が負か宝箱の確率が1を超えたら読まない()
        {
            const string text =
                "layer,area,crates_per_cell,chest_chance\n" +
                "1,20,-0.1,0.1\n" +
                "2,20,0.1,1.5\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);
            Assert.IsEmpty(rows);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("crates_per_cell", errors[0]);
            StringAssert.Contains("chest_chance", errors[1]);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "layer,area\n" +
                "1,27\n" +
                "0,27\n" +      // 層 0
                "2,0\n" +       // 面積 0
                "2,-5\n" +      // 負の面積
                "2,\n" +        // 面積が空
                "1,30\n" +      // 重複
                "2,30\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.Contains("重複", errors[4]);
        }

        [Test]
        public void 見出しに要る列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse("layer\n1\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("area", errors[0]);
        }

        [Test]
        public void 高さの列は無くても空でも読めて書いてあれば読む()
        {
            const string text =
                "layer,area,elevation\n" +
                "1,27,20\n" +
                "2,30,\n" +       // 空なら設定を変えない
                "3,30,-1\n" +     // 負の高さ
                "4,30,abc\n" +    // 数でない
                "5,30,0\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            Assert.IsTrue(rows[0].HasElevation);
            Assert.AreEqual(20f, rows[0].Elevation);
            Assert.IsFalse(rows[1].HasElevation);
            Assert.AreEqual(0f, rows[2].Elevation);

            List<FloorLayerCsv.Row> old = FloorLayerCsv.Parse("layer,area\n1,27\n", errors);
            Assert.IsFalse(old[0].HasElevation);
        }

        [Test]
        public void 部屋の平均面積は重みで平均し重み0の部屋は数えない()
        {
            var rooms = new List<RoomLayerCsv.Row>
            {
                new RoomLayerCsv.Row(2, 1, 1, 1, 1f, 0),
                new RoomLayerCsv.Row(3, 1, 1, 2, 0.8f, 0),
                new RoomLayerCsv.Row(4, 1, 2, 2, 0.5f, 0),
                new RoomLayerCsv.Row(5, 1, 5, 5, 0f, 0),
            };

            Assert.AreEqual(2d, FloorLayerCsv.MeanRoomArea(rooms), 1e-6);
            Assert.AreEqual(0d, FloorLayerCsv.MeanRoomArea(new List<RoomLayerCsv.Row>()));
        }

        [Test]
        public void 部屋数は上限が下限の2倍で平均が面積に合う()
        {
            // 特別な部屋 3 つ（1x1）＋ 普通の部屋 12 個（1 つあたり 2）= 平均 15 部屋 = 27 セル → 10〜20。
            Assert.AreEqual((10, 20), FloorLayerCsv.RoomCountRange(27f, 2d, 3, 3f, 4));

            // 面積を倍にすると普通の部屋が倍になる: 3 + 24 = 27 部屋 → 18〜36。
            Assert.AreEqual((18, 36), FloorLayerCsv.RoomCountRange(51f, 2d, 3, 3f, 4));

            // 狭すぎても下限は割らない。
            Assert.AreEqual((4, 8), FloorLayerCsv.RoomCountRange(2f, 2d, 3, 3f, 4));
        }

        [Test]
        public void ショップとショートカットの確率は空なら変えず_0から1の外なら読まない()
        {
            const string text =
                "layer,area,shop_chance,shortcut_chance\n" +
                "1,20,0.5,0.25\n" +
                "2,30,,\n" +
                "3,40,1.5,0\n";
            var errors = new List<string>();
            List<FloorLayerCsv.Row> rows = FloorLayerCsv.Parse(text, errors);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(0.5f, rows[0].ShopChance);
            Assert.AreEqual(0.25f, rows[0].ShortcutChance);
            Assert.IsFalse(rows[1].HasShopChance);
            Assert.IsFalse(rows[1].HasShortcutChance);
        }
    }
}
