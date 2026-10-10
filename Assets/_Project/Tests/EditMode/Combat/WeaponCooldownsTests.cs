using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>武器ごとの待ちの覚え方を確かめる。</summary>
    public sealed class WeaponCooldownsTests
    {
        [Test]
        public void 覚えていない武器は待たない()
        {
            var cooldowns = new WeaponCooldowns();
            Assert.That(cooldowns.Remaining(new object(), 0f), Is.EqualTo(0f));
        }

        [Test]
        public void 離れている間も待ちは減っていく()
        {
            var cooldowns = new WeaponCooldowns();
            var bow = new object();
            cooldowns.Store(bow, 10f, 2f);
            Assert.That(cooldowns.Remaining(bow, 10.5f), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(cooldowns.Remaining(bow, 12.5f), Is.EqualTo(0f));
        }

        [Test]
        public void 待ちは武器どうしで共有しない()
        {
            var cooldowns = new WeaponCooldowns();
            var bow = new object();
            var staff = new object();
            cooldowns.Store(bow, 0f, 3f);
            Assert.That(cooldowns.Remaining(staff, 0f), Is.EqualTo(0f));

            cooldowns.Store(staff, 1f, 1f);
            Assert.That(cooldowns.Remaining(bow, 1f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(cooldowns.Remaining(staff, 1f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void 素手も1つの武器として覚える()
        {
            var cooldowns = new WeaponCooldowns();
            cooldowns.Store(null, 0f, 1f);
            Assert.That(cooldowns.Remaining(null, 0.25f), Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void 待ちの無い物を覚え直すと忘れる()
        {
            var cooldowns = new WeaponCooldowns();
            var bow = new object();
            cooldowns.Store(bow, 0f, 3f);
            cooldowns.Store(bow, 1f, 0f);
            Assert.That(cooldowns.Remaining(bow, 1f), Is.EqualTo(0f));
        }
    }
}
