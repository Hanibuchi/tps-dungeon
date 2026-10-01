using NUnit.Framework;

namespace TpsDungeon.Items.Tests
{
    /// <summary>情報欄のエンチャントの 1 行（合計の効果量の書き方）を確かめる。</summary>
    public sealed class EnchantmentLabelTests
    {
        [Test]
        public void 数や多重は個数で出す()
        {
            Assert.AreEqual("数 +1", EnchantmentLabel.Format(EnchantmentKind.ProjectileCount, "数", 1f));
            Assert.AreEqual("数 +2", EnchantmentLabel.Format(EnchantmentKind.ProjectileCount, "数", 2f));
            Assert.AreEqual("多重 +3", EnchantmentLabel.Format(EnchantmentKind.Multishot, "多重", 3f));
        }

        [Test]
        public void 割合はパーセントで出す()
        {
            Assert.AreEqual("ダメージ増加 +15%", EnchantmentLabel.Format(EnchantmentKind.DamageUp, "ダメージ増加", 0.15f));
            Assert.AreEqual("ダメージ増加 +45%", EnchantmentLabel.Format(EnchantmentKind.DamageUp, "ダメージ増加", 0.15f * 3));
            Assert.AreEqual("クリティカル率 +5%", EnchantmentLabel.Format(EnchantmentKind.CritChance, "クリティカル率", 0.05f));
            Assert.AreEqual("サイズ +17.5%", EnchantmentLabel.Format(EnchantmentKind.Size, "サイズ", 0.175f));
        }

        [Test]
        public void 種類ごとの単位()
        {
            Assert.AreEqual("チャージ時間減少 -15%", EnchantmentLabel.Format(EnchantmentKind.ChargeTimeDown, "チャージ時間減少", 0.15f));
            Assert.AreEqual("コンボボーナス +10%/段", EnchantmentLabel.Format(EnchantmentKind.ComboBonus, "コンボボーナス", 0.1f));
            Assert.AreEqual("ノックバック +4 m/s", EnchantmentLabel.Format(EnchantmentKind.Knockback, "ノックバック", 4f));
            Assert.AreEqual("自然回復 +1/秒", EnchantmentLabel.Format(EnchantmentKind.Regen, "自然回復", 1f));
            Assert.AreEqual("爆発 40%（半径 2.5 m）", EnchantmentLabel.Format(EnchantmentKind.Explosion, "爆発", 0.4f, 2.5f));
            Assert.AreEqual("ホーミング", EnchantmentLabel.Format(EnchantmentKind.Homing, "ホーミング", 1f));
        }
    }
}
