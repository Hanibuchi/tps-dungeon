using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Party.Tests
{
    /// <summary>パーティーの並び順の規則（上限・並べ替え・先頭の交代の知らせ）を確かめる。</summary>
    public sealed class PartyOrderTests
    {
        private sealed class Member
        {
            public readonly string Name;
            public Member(string name) => Name = name;
            public override string ToString() => Name;
        }

        private static PartyOrder<Member> Make(int count, int capacity = 12)
        {
            var order = new PartyOrder<Member>(capacity);
            for (int i = 0; i < count; i++) order.Add(new Member(((char)('A' + i)).ToString()));
            return order;
        }

        private static string Names(PartyOrder<Member> order)
        {
            var names = new List<string>();
            foreach (Member m in order.Members) names.Add(m.Name);
            return string.Join("", names);
        }

        [Test]
        public void 上限の人数までしか入らない()
        {
            var order = Make(12);

            Assert.IsTrue(order.IsFull);
            Assert.IsFalse(order.Add(new Member("X")));
            Assert.AreEqual(12, order.Count);
        }

        [Test]
        public void 同じ人は二度入らない()
        {
            var order = new PartyOrder<Member>(12);
            var a = new Member("A");

            Assert.IsTrue(order.Add(a));
            Assert.IsFalse(order.Add(a));
            Assert.AreEqual(1, order.Count);
        }

        [Test]
        public void Moveは抜き出して指定の位置に入れる()
        {
            var order = Make(5);

            Assert.IsTrue(order.Move(3, 1));
            Assert.AreEqual("ADBCE", Names(order));

            Assert.IsTrue(order.Move(0, 4));
            Assert.AreEqual("DBCEA", Names(order));
        }

        [Test]
        public void Moveは範囲外や同じ位置では何もしない()
        {
            var order = Make(3);
            int changed = 0;
            order.Changed += _ => changed++;

            Assert.IsFalse(order.Move(1, 1));
            Assert.IsFalse(order.Move(-1, 0));
            Assert.IsFalse(order.Move(0, 3));
            Assert.AreEqual(0, changed);
            Assert.AreEqual("ABC", Names(order));
        }

        [Test]
        public void 先頭へ動かすと先頭の交代を知らせる()
        {
            var order = Make(4);
            Member before = null;
            Member after = null;
            int leaderChanged = 0;
            order.LeaderChanged += (b, a) =>
            {
                before = b;
                after = a;
                leaderChanged++;
            };

            order.Move(2, 0);

            Assert.AreEqual(1, leaderChanged);
            Assert.AreEqual("A", before.Name);
            Assert.AreEqual("C", after.Name);
            Assert.AreSame(after, order.Leader);
        }

        [Test]
        public void 先頭が変わらない並べ替えでは先頭の交代を知らせない()
        {
            var order = Make(4);
            int leaderChanged = 0;
            int changed = 0;
            order.LeaderChanged += (_, __) => leaderChanged++;
            order.Changed += _ => changed++;

            order.Move(3, 1);

            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, leaderChanged);
        }

        [Test]
        public void 先頭が抜けると次の人が先頭になる()
        {
            var order = Make(3);
            Member a = order.Leader;

            Assert.IsTrue(order.Remove(a));
            Assert.AreEqual("B", order.Leader.Name);
            Assert.AreEqual(2, order.Count);
        }
    }
}
