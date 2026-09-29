using System.Collections.Generic;
using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>近接の 4 段コンボの進み方を確かめる。</summary>
    public sealed class MeleeComboStateTests
    {
        private const float Grace = 0.25f;

        private static MeleeComboState Combo() => new MeleeComboState(new[]
        {
            new ComboStepTiming(0.45f, 0.18f),
            new ComboStepTiming(0.45f, 0.18f),
            new ComboStepTiming(0.5f, 0.2f),
            new ComboStepTiming(0.8f, 0.4f),
        }, Grace);

        /// <summary>今の段を最後まで振り切る。途中で起きたことを重ねて返す。</summary>
        private static ComboEvents Finish(MeleeComboState combo, float dt = 0.01f)
        {
            var events = ComboEvents.None;
            int step = combo.Step;
            for (int i = 0; i < 1000 && combo.IsSwinging && combo.Step == step; i++) events |= combo.Tick(dt, false);
            return events;
        }

        [Test]
        public void 待機中に押すと1段目が始まる()
        {
            var combo = Combo();
            Assert.AreEqual(-1, combo.Step);

            Assert.AreEqual(ComboEvents.StepStarted, combo.Tick(0.016f, true));
            Assert.AreEqual(0, combo.Step);
        }

        [Test]
        public void 判定の時刻を過ぎたフレームに一度だけHitが立つ()
        {
            var combo = Combo();
            combo.Tick(0f, true);

            Assert.AreEqual(ComboEvents.None, combo.Tick(0.1f, false));
            Assert.AreEqual(ComboEvents.Hit, combo.Tick(0.1f, false));
            Assert.AreEqual(ComboEvents.None, combo.Tick(0.1f, false));
        }

        [Test]
        public void 判定より前に押しても先行入力にならない()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            combo.Tick(0.05f, true);

            Finish(combo);
            Assert.IsFalse(combo.IsSwinging, "先行入力されていないので段の終わりで止まる");
            Assert.AreEqual(1, combo.NextStep);
        }

        [Test]
        public void 判定以降に押すと段の終わりで次の段へ続く()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            combo.Tick(0.2f, true);

            ComboEvents events = combo.Tick(0.3f, false);
            Assert.IsTrue((events & ComboEvents.StepStarted) != 0);
            Assert.AreEqual(1, combo.Step);
        }

        [Test]
        public void 段が終わってから猶予内に押せば次の段()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            Finish(combo);

            combo.Tick(Grace * 0.5f, false);
            combo.Tick(0.016f, true);
            Assert.AreEqual(1, combo.Step);
        }

        [Test]
        public void 猶予を過ぎたら1段目に戻る()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            Finish(combo);

            Assert.IsTrue((combo.Tick(Grace + 0.01f, false) & ComboEvents.Ended) != 0);
            combo.Tick(0.016f, true);
            Assert.AreEqual(0, combo.Step);
        }

        [Test]
        public void 連打すると4段振って1段目に戻る()
        {
            var combo = Combo();
            var started = new List<int>();
            int hits = 0;

            for (int frame = 0; frame < 400 && started.Count < 5; frame++)
            {
                ComboEvents events = combo.Tick(1f / 60f, frame % 4 == 0);
                if ((events & ComboEvents.StepStarted) != 0) started.Add(combo.Step);
                if ((events & ComboEvents.Hit) != 0) hits++;
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 0 }, started);
            Assert.AreEqual(4, hits);
        }

        [Test]
        public void 最後の段を振り切ると待機に戻り_次は1段目()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            for (int step = 1; step < 4; step++)
            {
                Finish(combo);
                combo.Tick(0f, true);
                Assert.AreEqual(step, combo.Step);
            }

            Assert.IsTrue((Finish(combo) & ComboEvents.Ended) != 0);
            Assert.AreEqual(0, combo.NextStep);
        }

        [Test]
        public void 攻撃速度を上げると早く判定が出る()
        {
            var slow = Combo();
            var fast = Combo();
            slow.Tick(0f, true);
            fast.Tick(0f, true);

            Assert.AreEqual(ComboEvents.None, slow.Tick(0.1f, false, 1f));
            Assert.AreEqual(ComboEvents.Hit, fast.Tick(0.1f, false, 2f));
        }

        [Test]
        public void Resetで待機と1段目に戻る()
        {
            var combo = Combo();
            combo.Tick(0f, true);
            combo.Tick(0.3f, true);
            combo.Reset();

            Assert.AreEqual(-1, combo.Step);
            Assert.AreEqual(0, combo.NextStep);
        }

        // ---- 待ち ----

        private const float Cooldown = 0.6f;

        private static MeleeComboState TwoStepCombo(float cooldown = Cooldown) => new MeleeComboState(new[]
        {
            new ComboStepTiming(0.4f, 0.2f),
            new ComboStepTiming(0.4f, 0.2f),
        }, Grace, cooldown);

        [Test]
        public void 最後の段のあとは待ちに入りその間の押下は捨てる()
        {
            var combo = TwoStepCombo();
            combo.Tick(0f, true);
            Finish(combo);
            combo.Tick(0f, true);
            Assert.IsTrue((Finish(combo) & ComboEvents.Ended) != 0);

            Assert.IsTrue(combo.IsCoolingDown);
            Assert.AreEqual(Cooldown, combo.CooldownDuration, 1e-5f);
            Assert.AreEqual(ComboEvents.None, combo.Tick(0.3f, true));
            Assert.IsFalse(combo.IsSwinging, "待ちの間は振れない");
            Assert.AreEqual(ComboEvents.None, combo.Tick(0.31f, false));

            Assert.IsFalse(combo.IsCoolingDown);
            Assert.AreEqual(ComboEvents.StepStarted, combo.Tick(0.016f, true));
            Assert.AreEqual(0, combo.Step, "明けたら 1 段目から");
        }

        [Test]
        public void 最後の段で先行入力しても待ちに入る()
        {
            var combo = TwoStepCombo();
            combo.Tick(0f, true);
            Finish(combo);
            combo.Tick(0f, true);
            combo.Tick(0.3f, true);

            Finish(combo);
            Assert.IsFalse(combo.IsSwinging);
            Assert.IsTrue(combo.IsCoolingDown);
        }

        [Test]
        public void 一段だけのコンボは振るたびに待ちに入る()
        {
            // ダッシュ突き・叩きつけ。連打しても 1 回ごとに待たせる。
            var combo = new MeleeComboState(new[] { new ComboStepTiming(0.75f, 0.2f) }, Grace, 0.3f);
            Assert.AreEqual(ComboEvents.StepStarted, combo.Tick(0f, true));
            combo.Tick(0.5f, true);

            Assert.IsTrue((Finish(combo) & ComboEvents.Ended) != 0);
            Assert.IsTrue(combo.IsCoolingDown);
            Assert.AreEqual(0, combo.NextStep);
        }

        [Test]
        public void 待ちが0なら最後の段の先行入力で1段目へ続く()
        {
            var combo = TwoStepCombo(0f);
            combo.Tick(0f, true);
            Finish(combo);
            combo.Tick(0f, true);
            combo.Tick(0.3f, true);

            Assert.IsTrue((combo.Tick(0.2f, false) & ComboEvents.StepStarted) != 0);
            Assert.AreEqual(0, combo.Step);
        }

        [Test]
        public void 猶予を過ぎてコンボが切れても待ちに入る()
        {
            var combo = TwoStepCombo();
            combo.Tick(0f, true);
            Finish(combo);
            Assert.IsFalse(combo.IsCoolingDown, "猶予の間はまだ待たない");

            Assert.IsTrue((combo.Tick(Grace + 0.01f, false) & ComboEvents.Ended) != 0);
            Assert.IsTrue(combo.IsCoolingDown);
        }

        [Test]
        public void 待ちは攻撃速度に関係なく実時間で減る()
        {
            var combo = TwoStepCombo();
            combo.StartCooldown(0.5f);

            combo.Tick(0.25f, false, 4f);
            Assert.AreEqual(0.25f, combo.CooldownRemaining, 1e-5f);
        }

        [Test]
        public void StartCooldownは今の残りより短ければ縮めない()
        {
            var combo = TwoStepCombo();
            combo.StartCooldown(1f);
            combo.StartCooldown(0.5f);
            Assert.AreEqual(1f, combo.CooldownRemaining, 1e-5f);
            Assert.AreEqual(1f, combo.CooldownDuration, 1e-5f);

            combo.StartCooldown(2f);
            Assert.AreEqual(2f, combo.CooldownDuration, 1e-5f);
        }
    }
}
