using System.Collections.Generic;
using NUnit.Framework;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Tests
{
    /// <summary>層ごとの部屋の敵の値（EnemySpawnLayers.csv）の読み取りを確かめる。</summary>
    public sealed class EnemySpawnLayerCsvTests
    {
        [Test]
        public void 見出しの名前で列を引いて層ごとの値を読む()
        {
            const string text =
                "# メモ\r\n" +
                "monster_house_multiplier,layer,note,budget_per_cell,monster_house_chance,single_weight\r\n" +
                "\r\n" +
                "2,1,浅い,2,0.2,0.5\r\n" +
                " 1.5 , 3 , , 4.5 , 0 , 0 \r\n";
            var errors = new List<string>();
            List<EnemySpawnLayerCsv.Row> rows = EnemySpawnLayerCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0].Layer);
            Assert.AreEqual(2f, rows[0].BudgetPerCell);
            Assert.AreEqual(0.2f, rows[0].MonsterHouseChance);
            Assert.AreEqual(2f, rows[0].MonsterHouseMultiplier);
            Assert.AreEqual(0.5f, rows[0].SingleWeight);
            Assert.AreEqual(4, rows[0].Line);
            Assert.AreEqual(3, rows[1].Layer);
            Assert.AreEqual(4.5f, rows[1].BudgetPerCell);
            Assert.AreEqual(0f, rows[1].MonsterHouseChance);
            Assert.AreEqual(1.5f, rows[1].MonsterHouseMultiplier);
            Assert.AreEqual(0f, rows[1].SingleWeight);
        }

        [Test]
        public void 読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "layer,budget_per_cell,monster_house_chance,monster_house_multiplier,single_weight\n" +
                "1,2,0.2,2,0.5\n" +
                "0,2,0.2,2,0.5\n" +     // 層 0
                "2,-1,0.2,2,0.5\n" +    // 負の予算
                "3,2,1.5,2,0.5\n" +     // 確率が 1 を超える
                "4,2,0.2,,0.5\n" +      // 倍率が空
                "5,2,0.2,2,-1\n" +      // 負の単種の重み
                "1,3,0.2,2,0.5\n";      // 重複
            var errors = new List<string>();
            List<EnemySpawnLayerCsv.Row> rows = EnemySpawnLayerCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(6, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
        }

        [Test]
        public void 見出しに要る列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<EnemySpawnLayerCsv.Row> rows = EnemySpawnLayerCsv.Parse("layer,budget_per_cell\n1,2\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("monster_house_chance", errors[0]);
            StringAssert.Contains("single_weight", errors[0]);
        }
    }
}
