using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Items.Tests
{
    /// <summary>持ち物の枠の並べ替えの規則を確かめる。ホットバー 4 枠＋バッグで組む。</summary>
    public sealed class InventoryTests
    {
        private const int Hotbar = 4;

        private ItemDefinition a;
        private ItemDefinition b;
        private ItemDefinition c;

        [SetUp]
        public void SetUp()
        {
            a = ScriptableObject.CreateInstance<ItemDefinition>();
            b = ScriptableObject.CreateInstance<ItemDefinition>();
            c = ScriptableObject.CreateInstance<ItemDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }

        // ---- 拾う ----------------------------------------------------------

        [Test]
        public void 拾ったものはホットバーの左から詰めて入り_埋まったらバッグへ()
        {
            var inventory = new Inventory(Hotbar, 12);
            for (int i = 0; i < Hotbar; i++) Assert.AreEqual(i, inventory.TryAdd(a));

            Assert.AreEqual(Hotbar, inventory.TryAdd(b), "ホットバーが埋まったらバッグの先頭");
            Assert.IsTrue(inventory.IsBag(Hotbar));
        }

        [Test]
        public void 途中の枠が空いていればそこから埋める()
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);
            inventory.TryAdd(a);
            inventory.TryAdd(a);
            inventory.RemoveAt(1);

            Assert.AreEqual(1, inventory.TryAdd(b));
        }

        [Test]
        public void 満杯なら入らない()
        {
            var inventory = new Inventory(Hotbar, 1);
            for (int i = 0; i < Hotbar + 1; i++) inventory.TryAdd(a);

            Assert.IsFalse(inventory.HasSpace);
            Assert.AreEqual(-1, inventory.TryAdd(b));
        }

        [Test]
        public void 空の参照は入れない()
        {
            var inventory = new Inventory(Hotbar, 12);
            Assert.AreEqual(-1, inventory.TryAdd(null));
            Assert.IsTrue(inventory.IsEmpty(0));
        }

        // ---- 移す ----------------------------------------------------------

        [Test]
        public void 空き枠へ移すと元の枠は空く()
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);

            Assert.IsTrue(inventory.Move(0, 10));
            Assert.IsNull(inventory[0]);
            Assert.AreSame(a, inventory[10]);
        }

        [Test]
        public void 埋まった枠へ移すと入れ替わる()
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);
            inventory.TryAdd(b);

            Assert.IsTrue(inventory.Move(0, 1));
            Assert.AreSame(b, inventory[0]);
            Assert.AreSame(a, inventory[1]);
        }

        [TestCase(0, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 16)]
        [TestCase(5, 0)]
        public void 同じ枠_範囲外_空の枠からは移さない(int from, int to)
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);

            Assert.IsFalse(inventory.Move(from, to));
            Assert.AreSame(a, inventory[0]);
        }

        // ---- 持ち替え（Shift＋クリック） -----------------------------------

        [Test]
        public void バッグのものはホットバーの左の空きへ持ち替える()
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);
            inventory.TryAdd(a);
            inventory.Move(1, 3);
            inventory.Move(0, 2);
            // ホットバー: [空, 空, a, a]
            Assert.IsTrue(inventory.Move(2, 9));
            // ホットバー: [空, 空, 空, a]、バッグの 9 に a

            Assert.IsTrue(inventory.QuickMove(9));
            Assert.AreSame(a, inventory[0]);
            Assert.IsNull(inventory[9]);
        }

        [Test]
        public void ホットバーのものはバッグの先頭の空きへ持ち替える()
        {
            var inventory = new Inventory(Hotbar, 12);
            for (int i = 0; i < Hotbar; i++) inventory.TryAdd(a);
            inventory.TryAdd(b); // バッグの先頭（4）
            inventory.TryAdd(c); // 1 番目のホットバーは埋まっているので 5
            inventory.RemoveAt(4);

            Assert.IsTrue(inventory.QuickMove(2));
            Assert.AreSame(a, inventory[4], "バッグの先頭の空き");
            Assert.IsNull(inventory[2]);
        }

        [Test]
        public void 持ち替え先に空きが無ければ動かない()
        {
            var inventory = new Inventory(Hotbar, 12);
            for (int i = 0; i < Hotbar; i++) inventory.TryAdd(a);
            inventory.TryAdd(b);

            Assert.IsFalse(inventory.QuickMove(Hotbar), "ホットバーが埋まっている");
            Assert.AreSame(b, inventory[Hotbar]);
        }

        [Test]
        public void 空の枠は持ち替えない()
        {
            var inventory = new Inventory(Hotbar, 12);
            Assert.IsFalse(inventory.QuickMove(Hotbar));
        }

        // ---- 捨てる・広げる -----------------------------------------------

        [Test]
        public void 取り出すと中身が返って枠が空く()
        {
            var inventory = new Inventory(Hotbar, 12);
            inventory.TryAdd(a);

            Assert.AreSame(a, inventory.RemoveAt(0));
            Assert.IsTrue(inventory.IsEmpty(0));
            Assert.IsNull(inventory.RemoveAt(0));
        }

        [Test]
        public void バッグを広げると中身はそのままで入るようになる()
        {
            var inventory = new Inventory(Hotbar, 0);
            for (int i = 0; i < Hotbar; i++) inventory.TryAdd(a);
            Assert.IsFalse(inventory.HasSpace);

            inventory.ExpandBag(2);

            Assert.AreEqual(2, inventory.BagCapacity);
            Assert.AreEqual(Hotbar + 2, inventory.Count);
            Assert.AreSame(a, inventory[0]);
            Assert.AreEqual(Hotbar, inventory.TryAdd(b));
        }

        // ---- 知らせ --------------------------------------------------------

        [Test]
        public void 変わったときだけ知らせる()
        {
            var inventory = new Inventory(Hotbar, 12);
            int count = 0;
            inventory.Changed += _ => count++;

            inventory.TryAdd(a);
            inventory.Move(0, 5);
            inventory.QuickMove(5);
            inventory.RemoveAt(0);
            inventory.ExpandBag(1);
            Assert.AreEqual(5, count);

            inventory.Move(0, 1);
            inventory.RemoveAt(0);
            inventory.QuickMove(0);
            inventory.ExpandBag(0);
            Assert.AreEqual(5, count, "何も変わらない操作では知らせない");
        }
    }
}
