using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Party.Tests
{
    /// <summary>仲間に配る見た目（角・耳・しっぽの組み合わせ）の規則を確かめる。</summary>
    public sealed class CharacterLookTests
    {
        [Test]
        public void 先頭の人には何も付けない()
        {
            Assert.IsTrue(CharacterLook.ForIndex(0).IsNone);
        }

        [Test]
        public void パーティーの12人の見た目は重ならず_2人目からは何か付く()
        {
            var seen = new HashSet<CharacterLook>();
            for (int i = 0; i < 12; i++)
            {
                CharacterLook look = CharacterLook.ForIndex(i);
                Assert.IsTrue(seen.Add(look), $"{i} 人目の {look} が重なった");
                if (i > 0) Assert.IsFalse(look.IsNone, $"{i} 人目に何も付いていない");
            }
        }

        [Test]
        public void 何か付ける組み合わせは全部重ならずに配れる()
        {
            var seen = new HashSet<CharacterLook>();
            for (int i = 1; i < CharacterLook.Combinations; i++) Assert.IsTrue(seen.Add(CharacterLook.ForIndex(i)));
        }

        [Test]
        public void 色の番号は範囲内に収まる()
        {
            for (int i = 0; i < CharacterLook.Combinations; i++)
            {
                CharacterLook look = CharacterLook.ForIndex(i);
                Assert.That(look.horn, Is.InRange(0, CharacterLook.HornColors));
                Assert.That(look.ear, Is.InRange(0, CharacterLook.EarColors));
                Assert.That(look.tail, Is.InRange(0, CharacterLook.TailColors));
            }
        }

        [Test]
        public void 範囲外の色は付けないに直す()
        {
            var look = new CharacterLook(9, -1, 2).Clamped;
            Assert.AreEqual(new CharacterLook(0, 0, 2), look);
        }
    }
}
