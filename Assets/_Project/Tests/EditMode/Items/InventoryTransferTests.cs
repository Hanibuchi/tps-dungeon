using NUnit.Framework;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Items.Tests
{
    /// <summary>手持ちと共有のバッグの間で物を動かす規則と、キャラの持ち物の窓口（PlayerInventory）の入れ方を確かめる。</summary>
    public sealed class InventoryTransferTests
    {
        private ItemInstance a;
        private ItemInstance b;
        private ItemInstance c;

        [SetUp]
        public void SetUp()
        {
            a = new ItemInstance(ScriptableObject.CreateInstance<ItemDefinition>());
            b = new ItemInstance(ScriptableObject.CreateInstance<ItemDefinition>());
            c = new ItemInstance(ScriptableObject.CreateInstance<ItemDefinition>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(a.Definition);
            Object.DestroyImmediate(b.Definition);
            Object.DestroyImmediate(c.Definition);
        }

        [Test]
        public void Moveは空いた枠へ移す()
        {
            var hand = new Inventory(4, 0);
            var bag = new Inventory(0, 12);
            bag.Place(3, a);

            Assert.IsTrue(InventoryTransfer.Move(bag, 3, hand, 1));
            Assert.IsNull(bag[3]);
            Assert.AreSame(a, hand[1]);
        }

        [Test]
        public void Moveは埋まった枠と入れ替える()
        {
            var hand = new Inventory(4, 0);
            var bag = new Inventory(0, 12);
            hand.Place(0, a);
            bag.Place(5, b);

            Assert.IsTrue(InventoryTransfer.Move(bag, 5, hand, 0));
            Assert.AreSame(b, hand[0]);
            Assert.AreSame(a, bag[5]);
        }

        [Test]
        public void Moveは空の枠や範囲外では動かさない()
        {
            var hand = new Inventory(4, 0);
            var bag = new Inventory(0, 12);
            hand.Place(0, a);

            Assert.IsFalse(InventoryTransfer.Move(bag, 0, hand, 1));
            Assert.IsFalse(InventoryTransfer.Move(hand, 0, bag, 12));
            Assert.AreSame(a, hand[0]);
        }

        [Test]
        public void Moveは同じ入れ物どうしならその中で入れ替える()
        {
            var hand = new Inventory(4, 0);
            hand.Place(0, a);
            hand.Place(2, b);

            Assert.IsTrue(InventoryTransfer.Move(hand, 0, hand, 2));
            Assert.AreSame(b, hand[0]);
            Assert.AreSame(a, hand[2]);
        }

        [Test]
        public void QuickMoveは手持ちからバッグの先頭の空きへ_バッグから手持ちの左の空きへ移す()
        {
            var hand = new Inventory(4, 0);
            var bag = new Inventory(0, 12);
            hand.Place(0, a);
            hand.Place(2, b);
            bag.Place(0, c);

            Assert.IsTrue(InventoryTransfer.QuickMove(hand, 2, hand, bag));
            Assert.AreSame(b, bag[1]);

            Assert.IsTrue(InventoryTransfer.QuickMove(bag, 0, hand, bag));
            Assert.AreSame(c, hand[1]);
        }

        [Test]
        public void QuickMoveは行き先が満杯なら動かさない()
        {
            var hand = new Inventory(1, 0);
            var bag = new Inventory(0, 1);
            hand.Place(0, a);
            bag.Place(0, b);

            Assert.IsFalse(InventoryTransfer.QuickMove(bag, 0, hand, bag));
            Assert.AreSame(a, hand[0]);
            Assert.AreSame(b, bag[0]);
        }

        [Test]
        public void PlayerInventoryのTryAddは手持ちの左から埋めてからバッグに入れる()
        {
            var go = new GameObject("character");
            try
            {
                var inventory = go.AddComponent<PlayerInventory>();
                var shared = new Inventory(0, 2);
                inventory.Bag = shared;

                for (int i = 0; i < PlayerHotbar.SlotCount; i++)
                    Assert.IsTrue(inventory.TryAdd(new ItemInstance(a.Definition)));
                Assert.IsTrue(inventory.TryAdd(b));

                Assert.AreEqual(PlayerHotbar.SlotCount, inventory.Inventory.Count);
                Assert.AreSame(b, shared[0]);
                Assert.IsTrue(inventory.HasSpace);

                Assert.IsTrue(inventory.TryAdd(c));
                Assert.IsFalse(inventory.HasSpace);
                Assert.IsFalse(inventory.TryAdd(new ItemInstance(a.Definition)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerInventoryは共有のバッグの変化も知らせる()
        {
            var go = new GameObject("character");
            try
            {
                var inventory = go.AddComponent<PlayerInventory>();
                var shared = new Inventory(0, 2);
                inventory.Bag = shared;
                int changed = 0;
                inventory.Changed += _ => changed++;

                shared.Place(1, a);

                Assert.AreEqual(1, changed);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
