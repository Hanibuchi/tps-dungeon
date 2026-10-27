using System.IO;
using NUnit.Framework;
using TpsDungeon.Items;
using TpsDungeon.Map.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace TpsDungeon.Party.Tests
{
    /// <summary>ラン中の状態のセーブ（RunSaveData・RunSaveStore）を確かめる。ファイルは一時フォルダに書く。</summary>
    public sealed class RunSaveTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "RunSaveTests_" + System.Guid.NewGuid().ToString("N"));
            RunSaveStore.PathOverride = Path.Combine(directory, RunSaveStore.FileName);
        }

        [TearDown]
        public void TearDown()
        {
            RunSaveStore.PathOverride = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static RunSaveData Sample()
        {
            var data = new RunSaveData { scene = "L1_Random", Floor = new DungeonFloor(0, 2), wipePending = true };
            data.order.Add("player");
            data.order.Add("ally_1");
            var hands = new MemberItemsSaveData { id = "player" };
            hands.slots.Add(new ItemSaveData { id = "sword" });
            hands.slots.Add(new ItemSaveData());
            data.hands.Add(hands);
            var gem = new ItemSaveData { id = "gem" };
            gem.enchantments.Add(new EnchantmentSaveData { id = "Enchant_Damage", level = 2 });
            data.bag.Add(gem);
            return data;
        }

        [Test]
        public void 書いて読むと同じ中身に戻る()
        {
            RunSaveStore.Save(Sample());

            Assert.IsTrue(RunSaveStore.Exists);
            Assert.IsTrue(RunSaveStore.TryLoad(out RunSaveData back));
            Assert.AreEqual("L1_Random", back.scene);
            Assert.AreEqual(new DungeonFloor(0, 2), back.Floor);
            Assert.IsTrue(back.wipePending);
            CollectionAssert.AreEqual(new[] { "player", "ally_1" }, back.order);

            MemberItemsSaveData hands = back.FindHands("player");
            Assert.IsNotNull(hands);
            Assert.AreEqual(2, hands.slots.Count);
            Assert.AreEqual("sword", hands.slots[0].id);
            Assert.IsTrue(hands.slots[1].IsEmpty);
            Assert.AreEqual("Enchant_Damage", back.bag[0].enchantments[0].id);
            Assert.AreEqual(2, back.bag[0].enchantments[0].level);
        }

        [Test]
        public void 階に居なければFloorはnull()
        {
            var data = new RunSaveData { scene = "Surface", Floor = null };
            Assert.IsTrue(RunSaveStore.TryFromJson(RunSaveStore.ToJson(data), out RunSaveData back));
            Assert.IsNull(back.Floor);
        }

        [Test]
        public void 上書きすると新しい方が残る()
        {
            RunSaveStore.Save(Sample());
            RunSaveStore.Save(new RunSaveData { scene = "Surface" });

            Assert.IsTrue(RunSaveStore.TryLoad(out RunSaveData back));
            Assert.AreEqual("Surface", back.scene);
            Assert.IsEmpty(back.order);
            Assert.IsFalse(File.Exists(RunSaveStore.FilePath + ".tmp"));
        }

        [Test]
        public void ファイルが無ければ読まない()
        {
            Assert.IsFalse(RunSaveStore.Exists);
            Assert.IsFalse(RunSaveStore.TryLoad(out _));
        }

        [Test]
        public void 壊れていたら警告を出して読まない()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(RunSaveStore.FilePath, "{ this is not json");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("壊れている"));
            Assert.IsFalse(RunSaveStore.TryLoad(out _));
        }

        [Test]
        public void 空のJSONや欠けた項目でも空のリストで読める()
        {
            Assert.IsFalse(RunSaveStore.TryFromJson("", out _));
            Assert.IsTrue(RunSaveStore.TryFromJson("{\"scene\":\"Surface\"}", out RunSaveData data));
            Assert.IsNotNull(data.order);
            Assert.IsNotNull(data.hands);
            Assert.IsNotNull(data.bag);
        }

        [Test]
        public void 消すとファイルが無くなる()
        {
            RunSaveStore.Save(Sample());
            RunSaveStore.Delete();
            Assert.IsFalse(RunSaveStore.Exists);
        }
    }
}
