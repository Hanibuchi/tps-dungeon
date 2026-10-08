using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Party.Tests
{
    /// <summary>
    /// 仲間が手持ちの 4 枠から武器を選ぶ規則を確かめる。
    /// 状況（近ければ近接・遠ければ遠距離）→ 使えるか（待ち中なら別の武器）→ 素手は攻撃の武器が無いときだけ、の順。
    /// </summary>
    public sealed class FollowerWeaponPickerTests
    {
        private const float Close = 4f;
        private const float Now = 10f;

        private static FollowerWeaponOption Melee(float readyAt = 0f) =>
            new FollowerWeaponOption { IsMelee = true, Range = 2f, ReadyAt = readyAt, Usable = true };

        private static FollowerWeaponOption Ranged(float readyAt = 0f, float range = 20f) =>
            new FollowerWeaponOption { IsRanged = true, Range = range, ReadyAt = readyAt, Usable = true };

        private static readonly FollowerWeaponOption Empty = default;

        private static int Pick(float distance, int current, params FollowerWeaponOption[] options) =>
            FollowerWeaponPicker.Pick(new List<FollowerWeaponOption>(options), distance, Close, Now, current);

        [Test]
        public void 敵が近ければ近接を選ぶ()
        {
            Assert.AreEqual(1, Pick(2f, 0, Ranged(), Melee(), Empty, Empty));
        }

        [Test]
        public void 敵が遠ければ遠距離を選ぶ()
        {
            Assert.AreEqual(0, Pick(10f, 1, Ranged(), Melee(), Empty, Empty));
        }

        [Test]
        public void 優先する近接が待ち中なら使える遠距離にする()
        {
            Assert.AreEqual(0, Pick(2f, 1, Ranged(), Melee(readyAt: Now + 1f), Empty, Empty));
        }

        [Test]
        public void 優先する遠距離が待ち中なら使える近接にする()
        {
            Assert.AreEqual(1, Pick(10f, 0, Ranged(readyAt: Now + 1f), Melee(), Empty, Empty));
        }

        [Test]
        public void 同じ種類の別の武器が使えればそちらにする()
        {
            Assert.AreEqual(2, Pick(10f, 0, Ranged(readyAt: Now + 1f), Melee(readyAt: Now + 1f), Ranged(), Empty));
        }

        [Test]
        public void どれも待ち中なら優先する種類で一番早く使える物を持って待つ()
        {
            Assert.AreEqual(2, Pick(10f, 0, Ranged(readyAt: Now + 3f), Melee(readyAt: Now + 0.5f), Ranged(readyAt: Now + 1f), Empty));
        }

        [Test]
        public void 届かない遠距離は使えない扱い()
        {
            // 敵は 10 m 先。射程 5 m の遠距離は使えず、近接で寄る。
            Assert.AreEqual(1, Pick(10f, 0, Ranged(range: 5f), Melee(), Empty, Empty));
        }

        [Test]
        public void 近接しか無ければ遠い敵にも近接で寄る()
        {
            Assert.AreEqual(3, Pick(10f, 0, Empty, Empty, Empty, Melee()));
        }

        [Test]
        public void 同じ条件なら今持っている武器のまま()
        {
            Assert.AreEqual(2, Pick(2f, 2, Melee(), Empty, Melee(), Empty));
        }

        [Test]
        public void 同じ条件で今の武器でなければ左の枠()
        {
            Assert.AreEqual(1, Pick(2f, 0, Empty, Melee(), Melee(), Empty));
        }

        [Test]
        public void 攻撃の武器が無ければ素手()
        {
            Assert.AreEqual(FollowerWeaponPicker.Unarmed, Pick(2f, 0, Empty, Empty, Empty, Empty));
        }

        [Test]
        public void 使ってはいけない武器は選ばない()
        {
            // 治癒の場（誰も傷ついていない）は選ばず、ほかの武器にする。
            FollowerWeaponOption heal = Ranged();
            heal.Usable = false;
            Assert.AreEqual(1, Pick(10f, 0, heal, Melee(), Empty, Empty));
        }

        [Test]
        public void 使ってよい武器が無くても攻撃の武器があれば素手にはしない()
        {
            FollowerWeaponOption heal = Ranged();
            heal.Usable = false;
            Assert.AreEqual(0, Pick(10f, 1, heal, Empty, Empty, Empty));
        }
    }
}
