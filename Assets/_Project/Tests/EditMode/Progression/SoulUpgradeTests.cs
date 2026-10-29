using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>魂の強化の表（SoulUpgradeCsv）・木（SoulUpgradeTree）・並べ方（RadialTreeLayout）・ソウルの式と、保存（MetaUpgrades）を確かめる。</summary>
    public sealed class SoulUpgradeTests
    {
        private const string Csv =
            "id,name,parent,cost,effect,value,description\n" +
            "# コメント\n" +
            "root,根,,5,exp_rate,0.1,\n" +
            "hp_1,体力 I,root,10,max_hp,0.1,\"丈夫に, なる\"\n" +
            "hp_2,体力 II,hp_1,30,max_hp,0.2,\n" +
            "ally,仲間,root,15,party_size,1,\n" +
            "path_2,近道,ally,20,shortcut,2,\n" +
            "exp_1,経験 I,root,5,exp_rate,0.1,\n" +
            "exp_2,経験 II,exp_1,5,exp_rate,0.3,\n" +
            "bonus,ボーナス,root,5,floor_clear_exp,30,\n";

        private string path;
        private SoulUpgradeTable table;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "tps-dungeon-soul-upgrade-tests.json");
            if (File.Exists(path)) File.Delete(path);
            MetaUpgrades.PathOverride = path;

            table = ScriptableObject.CreateInstance<SoulUpgradeTable>();
            table.SetNodes(SoulUpgradeCsv.Parse(Csv, null));
            MetaUpgrades.TableOverride = table;
        }

        [TearDown]
        public void TearDown()
        {
            MetaUpgrades.PathOverride = null;
            MetaUpgrades.TableOverride = null;
            Object.DestroyImmediate(table);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void Csv_ReadsRowsAndQuotedCells()
        {
            var errors = new List<string>();
            List<SoulUpgradeNode> rows = SoulUpgradeCsv.Parse(Csv, errors);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(8, rows.Count);
            Assert.AreEqual("丈夫に, なる", rows[1].description);
            Assert.AreEqual(SoulUpgradeEffect.MaxHp, rows[1].effect);
            Assert.AreEqual(0.1f, rows[1].value, 1e-5f);
        }

        [Test]
        public void Csv_SkipsBadRowsWithErrors()
        {
            var errors = new List<string>();
            List<SoulUpgradeNode> rows = SoulUpgradeCsv.Parse(
                "id,name,parent,cost,effect,value,description\n" +
                "a,A,,x,exp_rate,0.1,\n" +
                "b,B,,1,unknown,0.1,\n" +
                "c,C,,1,exp_rate,0.1,\n" +
                "c,C2,,1,exp_rate,0.1,\n", errors);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(3, errors.Count);
        }

        [Test]
        public void Tree_DropsRowsThatDoNotReachTheRoot()
        {
            var errors = new List<string>();
            var tree = new SoulUpgradeTree(new[]
            {
                new SoulUpgradeNode { id = "root" },
                new SoulUpgradeNode { id = "a", parent = "root" },
                new SoulUpgradeNode { id = "lost", parent = "nowhere" },
                new SoulUpgradeNode { id = "x", parent = "y" },
                new SoulUpgradeNode { id = "y", parent = "x" },
            }, errors);
            Assert.AreEqual("root", tree.Root);
            Assert.AreEqual(2, tree.Count);
            Assert.AreEqual(3, errors.Count);
            Assert.AreEqual(1, tree.Depth("a"));
        }

        [Test]
        public void Tree_ShowsOnlyChildrenOfPurchased()
        {
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(Csv, null));
            var purchased = new HashSet<string>();
            Assert.IsTrue(tree.IsVisible("root", purchased));
            Assert.IsFalse(tree.IsVisible("hp_1", purchased));
            Assert.AreEqual(SoulUpgradeCheck.Hidden, tree.Check("hp_1", purchased, 999));

            purchased.Add("root");
            Assert.IsTrue(tree.IsVisible("hp_1", purchased));
            Assert.IsFalse(tree.IsVisible("hp_2", purchased));
            Assert.AreEqual(SoulUpgradeCheck.NotEnough, tree.Check("hp_1", purchased, 9));
            Assert.AreEqual(SoulUpgradeCheck.Ok, tree.Check("hp_1", purchased, 10));
            Assert.AreEqual(SoulUpgradeCheck.Owned, tree.Check("root", purchased, 10));
        }

        [Test]
        public void Tree_ValueIsTheAmountUpToThatStep()
        {
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(Csv, null));
            var purchased = new HashSet<string> { "root", "hp_1", "hp_2", "ally", "path_2" };
            Assert.AreEqual(0.2f, tree.Total(SoulUpgradeEffect.MaxHp, purchased), 1e-5f, "体力 0.1 → 0.2 の 2 段で +20%");
            Assert.AreEqual(0.1f, tree.Increment("hp_2"), 1e-5f);
            Assert.IsTrue(tree.Has(SoulUpgradeEffect.PartySize, purchased));
            CollectionAssert.AreEqual(new[] { 2f }, tree.Values(SoulUpgradeEffect.Shortcut, purchased));
        }

        [Test]
        public void Tree_RootDoesNotChainIntoTheSameEffect()
        {
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(Csv, null));
            Assert.AreEqual(0.1f, tree.Total(SoulUpgradeEffect.ExpRate, new HashSet<string> { "root" }), 1e-5f);
            Assert.AreEqual(0.2f, tree.Total(SoulUpgradeEffect.ExpRate, new HashSet<string> { "root", "exp_1" }), 1e-5f,
                "根の +10% と、枝の 1 段目の +10%");
            Assert.AreEqual(0.4f, tree.Total(SoulUpgradeEffect.ExpRate, new HashSet<string> { "root", "exp_1", "exp_2" }), 1e-5f,
                "枝は 2 段目で +30%（差分ではない）");
        }

        [Test]
        public void MetaUpgrades_FloorClearExpIsFixedAmountOfTopTier()
        {
            Assert.AreEqual(0, MetaUpgrades.FloorClearExp, "解放していなければ 0");

            MetaUpgrades.AddSouls(100);
            MetaUpgrades.TryBuy("root");
            MetaUpgrades.TryBuy("bonus");
            Assert.AreEqual(30, MetaUpgrades.FloorClearExp);

            var modifiers = new ProgressionModifiers();
            MetaUpgrades.ApplyTo(modifiers);
            Assert.AreEqual(2f, modifiers.ExpMultiplier, 1e-5f, "経験値の倍率には入らない（root の経験値増加 1 つ分だけ）");
        }

        [Test]
        public void Layout_PutsRootInTheMiddleAndChildrenOnRings()
        {
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(Csv, null));
            Dictionary<string, RadialTreeLayout.Point> points = RadialTreeLayout.Layout(tree);
            Assert.AreEqual(tree.Count, points.Count);
            Assert.AreEqual(0f, points["root"].X, 1e-4f);
            Assert.AreEqual(0f, points["root"].Y, 1e-4f);
            Assert.AreEqual(RadialTreeLayout.FirstRing, Length(points["hp_1"]), 0.01f);
            Assert.AreEqual(RadialTreeLayout.FirstRing + RadialTreeLayout.RingStep, Length(points["hp_2"]), 0.01f);
        }

        [Test]
        public void Layout_PutsTheNextRowInTheMiddleOfItsSiblings()
        {
            const string csv =
                "id,name,parent,cost,effect,value,description\n" +
                "root,根,,1,max_hp,0,\n" +
                "b_1,B I,root,1,crit_chance,0.1,\n" +
                "b_2,B II,b_1,1,crit_chance,0.2,\n" +
                "c_1,C I,b_1,1,crit_multiplier,0.1,\n" +
                "d_1,D I,b_1,1,bag_size,1,\n";
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(csv, null));
            Dictionary<string, RadialTreeLayout.Point> points = RadialTreeLayout.Layout(tree);

            // b_1 のすぐ次の行 b_2 は、表では先頭でも 3 つの真ん中に来る。
            Assert.Less((points["c_1"].Angle - points["b_2"].Angle) * (points["d_1"].Angle - points["b_2"].Angle), 0f);
        }

        [Test]
        public void StepsBeyondVisible_CountsFromTheNearestVisibleUpgrade()
        {
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(Csv, null));
            var purchased = new HashSet<string> { "root" };
            Assert.AreEqual(0, tree.StepsBeyondVisible("root", purchased));
            Assert.AreEqual(0, tree.StepsBeyondVisible("hp_1", purchased));
            Assert.AreEqual(1, tree.StepsBeyondVisible("hp_2", purchased));
            Assert.AreEqual(1, tree.StepsBeyondVisible("path_2", purchased));
            purchased.Clear();
            Assert.AreEqual(2, tree.StepsBeyondVisible("hp_2", purchased));
            Assert.AreEqual(-1, tree.StepsBeyondVisible("none", purchased));
        }

        [Test]
        public void Layout_OfTheRealTableDoesNotOverlap()
        {
            string csv = File.ReadAllText("Assets/_Project/Settings/Tables/SoulUpgrades.csv");
            var errors = new List<string>();
            var tree = new SoulUpgradeTree(SoulUpgradeCsv.Parse(csv, errors), errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));

            var points = new List<RadialTreeLayout.Point>(RadialTreeLayout.Layout(tree).Values);
            float closest = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    closest = Mathf.Min(closest, new Vector2(points[i].X - points[j].X, points[i].Y - points[j].Y).magnitude);
                }
            }
            // 強化の丸（直径 60）と値段の文字が重ならない間。
            Assert.Greater(closest, 90f);
        }

        [Test]
        public void MetaUpgrades_BuysAndSaves()
        {
            MetaUpgrades.AddSouls(20);
            Assert.AreEqual(SoulUpgradeCheck.Hidden, MetaUpgrades.TryBuy("hp_1"));
            Assert.AreEqual(SoulUpgradeCheck.Ok, MetaUpgrades.TryBuy("root"));
            Assert.AreEqual(15, MetaUpgrades.Souls);
            Assert.AreEqual(SoulUpgradeCheck.Ok, MetaUpgrades.TryBuy("hp_1"));
            Assert.AreEqual(SoulUpgradeCheck.NotEnough, MetaUpgrades.TryBuy("ally"));

            MetaUpgrades.Reload();
            Assert.AreEqual(5, MetaUpgrades.Souls);
            Assert.IsTrue(MetaUpgrades.IsPurchased("hp_1"));
            Assert.AreEqual(0.1f, MetaUpgrades.Total(SoulUpgradeEffect.MaxHp), 1e-5f);
        }

        [Test]
        public void MetaUpgrades_HarvestAddsSoulsAndKeepsTheBreakdownUntilShown()
        {
            long gained = MetaUpgrades.Harvest(new[]
            {
                new SoulHarvestMember { id = "player", name = "主人公", level = 10 },
                new SoulHarvestMember { id = "member_1", name = "仲間", level = 1 },
            });
            Assert.AreEqual(100_000L + 10L, gained, "Lv10 の 10 × 10⁴ と Lv1 の 10");
            Assert.AreEqual(gained, MetaUpgrades.Souls);
            Assert.IsTrue(MetaUpgrades.HasHarvest);
            Assert.AreEqual(2, MetaUpgrades.PendingHarvest.members.Count);

            MetaUpgrades.Reload();
            Assert.IsTrue(MetaUpgrades.HasHarvest, "見せる前に終えても残る");

            MetaUpgrades.ClearHarvest();
            MetaUpgrades.Reload();
            Assert.IsFalse(MetaUpgrades.HasHarvest);
            Assert.AreEqual(gained, MetaUpgrades.Souls, "見せ終えてもソウルは残る");
        }

        [Test]
        public void MetaUpgrades_AppliesModifiers()
        {
            MetaUpgrades.AddSouls(100);
            MetaUpgrades.TryBuy("root");
            MetaUpgrades.TryBuy("hp_1");

            var modifiers = new ProgressionModifiers();
            MetaUpgrades.ApplyTo(modifiers);
            Assert.AreEqual(2f, modifiers.ExpMultiplier, 1e-5f, "経験値増加を 1 つ解放して ×2");
            Assert.AreEqual(0.1f, modifiers.MaxHp.Percent, 1e-5f);

            MetaUpgrades.TryBuy("exp_1");
            MetaUpgrades.TryBuy("exp_2");
            MetaUpgrades.ApplyTo(modifiers);
            Assert.AreEqual(8f, modifiers.ExpMultiplier, 1e-5f, "倍率は値ではなく解放した数で決まる（3 つで ×8）");
        }

        private static float Length(RadialTreeLayout.Point p) => new Vector2(p.X, p.Y).magnitude;
    }
}
