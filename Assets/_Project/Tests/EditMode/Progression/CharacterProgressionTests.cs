using NUnit.Framework;
using TpsDungeon.Hud;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Progression.Tests
{
    /// <summary>レベルに応じた MaxHP・基礎攻撃力が PlayerHealth と HUD に届くか。</summary>
    public sealed class CharacterProgressionTests
    {
        private GameObject go;
        private PlayerHealth health;
        private CharacterProgression character;
        private CharacterGrowthProfile profile;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Player");
            health = go.AddComponent<PlayerHealth>();
            character = go.AddComponent<CharacterProgression>();
            profile = ScriptableObject.CreateInstance<CharacterGrowthProfile>();
            character.Configure(profile, "player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void StartsAtLevelOneWithFullHealth()
        {
            Assert.AreEqual(1, character.Level);
            Assert.AreEqual(100, health.MaxHp);
            Assert.AreEqual(100, health.CurrentHp);
            Assert.AreEqual(10f, character.GetBaseAttack(), 0.001f);
        }

        [Test]
        public void LevelUp_RaisesMaxAndCurrentByTheIncrease()
        {
            health.Damage(30);
            int healthChanges = 0;
            int maxFrom = 0, maxTo = 0;
            health.Changed += _ => healthChanges++;
            character.MaxHpChanged += (_, from, to) => { maxFrom = from; maxTo = to; };

            character.AddExp(2850); // 1 回で Lv1→10

            Assert.AreEqual(10, character.Level);
            Assert.AreEqual(129, health.MaxHp);
            Assert.AreEqual(70 + 29, health.CurrentHp, "増えた 29 だけ足す");
            Assert.AreEqual(100, maxFrom);
            Assert.AreEqual(129, maxTo);
            Assert.AreEqual(11.6f, character.BaseAttack, 0.05f);
            Assert.AreEqual("99 / 129", GameHudView.HpText(health.CurrentHp, health.MaxHp), "HUD の表示は新しい最大を指す");
            Assert.Greater(healthChanges, 0, "HUD が購読している Changed が飛ぶ");
        }

        [Test]
        public void LevelUp_FullHealMode_Refills()
        {
            profile.Configure(GrowthCurve.Default, LevelUpHpMode.FullHeal);
            character.Configure(profile);
            health.Damage(60);

            character.AddExp(10);

            Assert.AreEqual(2, character.Level);
            Assert.AreEqual(health.MaxHp, health.CurrentHp);
        }

        [Test]
        public void MultiLevelGain_NotifiesOnceWithRange()
        {
            int count = 0, from = 0, to = 0;
            character.LeveledUp += (_, f, t) => { count++; from = f; to = t; };

            character.AddExp(305);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, from);
            Assert.AreEqual(5, to);
        }

        [Test]
        public void Reset_BackToLevelOneAndFullHealth()
        {
            character.AddLevels(20);
            health.Damage(50);

            character.ResetProgress();

            Assert.AreEqual(1, character.Level);
            Assert.AreEqual(0, character.Exp);
            Assert.AreEqual(100, health.MaxHp);
            Assert.AreEqual(100, health.CurrentHp);
        }

        [Test]
        public void Modifiers_ScaleMaxHpAndAttack()
        {
            var modifiers = new ProgressionModifiers();
            character.SetModifiers(modifiers);

            modifiers.SetMaxHp(new StatModifier(20f, 0.5f));
            modifiers.SetBaseAttack(new StatModifier(0f, 0.1f));

            Assert.AreEqual(180, health.MaxHp, "(100 + 20) × 1.5");
            Assert.AreEqual(180, health.CurrentHp, "満タンなら増えた分も満タン");
            Assert.AreEqual(11f, character.BaseAttack, 0.001f);

            character.SetModifiers(null);
            Assert.AreEqual(100, health.MaxHp, "外すと元に戻る");
        }

        [Test]
        public void CaptureAndRestore_RoundTrip()
        {
            character.AddExp(355); // Lv5 + 55
            health.Damage(40);
            var data = character.Capture();

            character.ResetProgress();
            character.Restore(data);

            Assert.AreEqual("player", data.id);
            Assert.AreEqual(5, character.Level);
            Assert.AreEqual(55, character.Exp);
            Assert.AreEqual(data.currentHp, health.CurrentHp);
            Assert.AreEqual(GrowthCurve.Default.MaxHp(5), health.MaxHp);
        }

        [Test]
        public void Health_SetMaxAndCurrent_NotifiesOnce()
        {
            int changes = 0;
            health.Changed += _ => changes++;

            health.SetMaxAndCurrent(150, 120);

            Assert.AreEqual(150, health.MaxHp);
            Assert.AreEqual(120, health.CurrentHp);
            Assert.AreEqual(1, changes);

            health.SetMaxAndCurrent(150, 999);
            Assert.AreEqual(150, health.CurrentHp, "最大に収める");
        }
    }
}
