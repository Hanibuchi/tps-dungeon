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
                "c,1,-1\n" +            // 負の層
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

        [Test]
        public void 強さの列を読み_空欄はnullにする()
        {
            const string text =
                "id,cost,layers,hp,damage,move_speed,attack_interval,windup,attack_range,area_radius,detect_range,exp\n" +
                "goblin,2,1,40,10,3,1.4,0.5,1.8,,14,8\n" +
                "lava_slime,1,5,147,84,4,,3,2.2,4,14,49\n";
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            EnemyCsv.Stats goblin = rows[0].Stats;
            Assert.AreEqual(40, goblin.Hp);
            Assert.AreEqual(10, goblin.Damage);
            Assert.AreEqual(3f, goblin.MoveSpeed);
            Assert.AreEqual(1.4f, goblin.AttackInterval);
            Assert.AreEqual(0.5f, goblin.Windup);
            Assert.AreEqual(1.8f, goblin.AttackRange);
            Assert.IsNull(goblin.AreaRadius);
            Assert.AreEqual(14f, goblin.DetectRange);
            Assert.AreEqual(8, goblin.Exp);

            EnemyCsv.Stats slime = rows[1].Stats;
            Assert.IsNull(slime.AttackInterval);
            Assert.AreEqual(4f, slime.AreaRadius);
        }

        [Test]
        public void 強さの列が無ければ全部null()
        {
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse("id,cost,layers\ngoblin,2,1\n", errors);

            Assert.IsEmpty(errors);
            Assert.IsNull(rows[0].Stats.Hp);
            Assert.IsNull(rows[0].Stats.MoveSpeed);
            Assert.IsNull(rows[0].Stats.Exp);
        }

        [Test]
        public void 強さが読めない行はエラーに積んで飛ばす()
        {
            const string text =
                "id,cost,layers,hp,damage,move_speed,exp\n" +
                "ok,1,1,40,10,3,8\n" +
                "a,1,1,0,10,3,8\n" +     // hp は 1 以上
                "b,1,1,40,-1,3,8\n" +    // ダメージが負
                "c,1,1,40,10,abc,8\n" +  // 数でない
                "d,1,1,40.5,10,3,8\n" +  // hp は整数
                "e,1,1,40,10,0,8\n";     // 速さが 0
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("ok", rows[0].Id);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.StartsWith("3 行目: a の hp", errors[0]);
            StringAssert.Contains("move_speed", errors[2]);
        }

        [TestCase("", new int[0])]
        [TestCase("2", new[] { 2 })]
        [TestCase("0", new[] { 0 })]
        [TestCase("0-1", new[] { 0, 1 })]
        [TestCase("1-3", new[] { 1, 2, 3 })]
        [TestCase("5;1-2;2", new[] { 1, 2, 5 })]
        [TestCase(" 1 - 2 ; 4 ", new[] { 1, 2, 4 })]
        public void 層の書き方(string text, int[] expected)
        {
            Assert.IsTrue(EnemyCsv.TryParseLayers(text, out int[] layers, out string error), error);
            CollectionAssert.AreEqual(expected, layers);
        }

        [Test]
        public void kind_を読み_空欄は_Unknown_大文字小文字は問わない()
        {
            const string text =
                "id,kind,cost,layers\n" +
                "a,melee,1,1\n" +
                "b,Ranged,1,1\n" +
                "c, poison_gas ,1,1\n" +
                "d,,1,1\n";
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse(text, errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(4, rows.Count);
            Assert.AreEqual(EnemyKind.Melee, rows[0].Kind);
            Assert.AreEqual(EnemyKind.Ranged, rows[1].Kind);
            Assert.AreEqual(EnemyKind.PoisonGas, rows[2].Kind);
            Assert.AreEqual(EnemyKind.Unknown, rows[3].Kind);
        }

        [Test]
        public void kind_の列が無ければ_Unknown()
        {
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse("id,cost,layers\na,1,1\n", errors);

            Assert.IsEmpty(errors);
            Assert.AreEqual(EnemyKind.Unknown, rows[0].Kind);
        }

        [Test]
        public void 知らない_kind_はエラーに積んで飛ばす()
        {
            var errors = new List<string>();
            List<EnemyCsv.Row> rows = EnemyCsv.Parse("id,kind,cost,layers\na,melee,1,1\nb,sniper,1,1\n", errors);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("a", rows[0].Id);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("3 行目", errors[0]);
            StringAssert.Contains("sniper", errors[0]);
        }

        [Test]
        public void kind_の名前は読み書きで往復する()
        {
            foreach (EnemyKind kind in new[] { EnemyKind.Melee, EnemyKind.Ranged, EnemyKind.Leap, EnemyKind.Bomber, EnemyKind.PoisonGas, EnemyKind.Spawner })
            {
                Assert.IsTrue(EnemyCsv.TryParseKind(EnemyCsv.KindName(kind), out EnemyKind parsed));
                Assert.AreEqual(kind, parsed);
            }
        }
    }
}
