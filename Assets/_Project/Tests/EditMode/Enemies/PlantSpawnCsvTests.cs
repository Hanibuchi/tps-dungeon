using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Enemies.Tests
{
    /// <summary>草花の敵の CSV（PlantSpawns.csv）の読み取り。</summary>
    public sealed class PlantSpawnCsvTests
    {
        [Test]
        public void 見出しの名前で列を引き_上から順に読む()
        {
            const string text =
                "# 草花の敵\n" +
                "chance,enemy,memo\n" +
                "0.05,field_imp,跳ぶ\n" +
                "\n" +
                "0.01 , needle_bee\n";
            var errors = new List<string>();
            List<PlantSpawnCsv.Row> rows = PlantSpawnCsv.Parse(text, errors);

            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("field_imp", rows[0].EnemyId);
            Assert.AreEqual(0.05f, rows[0].Chance, 1e-6f);
            Assert.AreEqual(3, rows[0].Line);
            Assert.AreEqual("needle_bee", rows[1].EnemyId);
            Assert.AreEqual(0.01f, rows[1].Chance, 1e-6f);
        }

        [Test]
        public void 読めない行は飛ばして行番号付きでエラーに積む()
        {
            const string text =
                "enemy,chance\n" +
                "a,0.1\n" +
                ",0.1\n" +       // enemy が空
                "b,x\n" +        // 数でない
                "c,1.5\n" +      // 1 を超える
                "d,-0.1\n" +     // 負
                "a,0.2\n";       // 重複
            var errors = new List<string>();
            List<PlantSpawnCsv.Row> rows = PlantSpawnCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("a", rows[0].EnemyId);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目", errors[0]);
            StringAssert.StartsWith("7 行目", errors[4]);
        }

        [Test]
        public void 見出しに列が無ければ何も読まない()
        {
            var errors = new List<string>();
            List<PlantSpawnCsv.Row> rows = PlantSpawnCsv.Parse("enemy\na\n", errors);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("chance", errors[0]);
        }
    }
}
