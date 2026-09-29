using System;
using System.Collections.Generic;

namespace TpsDungeon.Combat
{
    /// <summary>コンボ 1 段の時間。秒（速射の補正前）。</summary>
    public readonly struct ComboStepTiming
    {
        public readonly float Duration;
        public readonly float HitTime;

        public ComboStepTiming(float duration, float hitTime)
        {
            Duration = Math.Max(0.01f, duration);
            HitTime = Math.Max(0f, Math.Min(hitTime, Duration));
        }
    }

    /// <summary><see cref="MeleeComboState.Tick"/> の 1 回で起きたこと。</summary>
    [Flags]
    public enum ComboEvents
    {
        None = 0,
        /// <summary>段が始まった（<see cref="MeleeComboState.Step"/> が新しい段）。</summary>
        StepStarted = 1,
        /// <summary>今の段の判定を出す瞬間を過ぎた。</summary>
        Hit = 2,
        /// <summary>コンボが切れて待機に戻った。</summary>
        Ended = 4,
    }

    /// <summary>
    /// 近接の N 段コンボの進み方。UnityEngine に依存しない。
    ///
    ///   待機中に押す              → 1 段目
    ///   振っている段の判定以降に押す → 先行入力。段が終わったらすぐ次の段
    ///   段が終わってから猶予内に押す → 次の段
    ///   猶予を過ぎる              → 待機（次は 1 段目から）
    ///   最後の段のあと            → 1 段目に戻る（先行入力があれば続けて 1 段目）
    ///   待機に戻ったとき           → cooldown 秒の待ち。その間の押下は捨てる（先行入力にもしない）
    ///                               最後の段のあとは先行入力があっても待つ（連打で待ちを飛ばさせない）
    ///
    /// 時間は speed（攻撃速度の倍率）を掛けて進める。猶予と待ちは実時間のまま。
    /// 持ち替えのように外から待たせたいときは <see cref="StartCooldown"/>。
    /// </summary>
    public sealed class MeleeComboState
    {
        private readonly ComboStepTiming[] steps;
        private readonly float chainGrace;
        private readonly float cooldown;

        private float stepTime;
        private float graceRemaining;
        private bool buffered;
        private int nextStep;

        public MeleeComboState(IReadOnlyList<ComboStepTiming> steps, float chainGrace, float cooldown = 0f)
        {
            if (steps == null || steps.Count == 0) throw new ArgumentException("段が 1 つも無い", nameof(steps));

            this.steps = new ComboStepTiming[steps.Count];
            for (int i = 0; i < steps.Count; i++) this.steps[i] = steps[i];
            this.chainGrace = Math.Max(0f, chainGrace);
            this.cooldown = Math.Max(0f, cooldown);
            Reset();
        }

        public int StepCount => steps.Length;

        /// <summary>振っている段（0 始まり）。振っていなければ -1。</summary>
        public int Step { get; private set; }

        /// <summary>段を振っている最中か。</summary>
        public bool IsSwinging => Step >= 0;

        /// <summary>今の段の中での経過（秒、速度補正後の時計）。</summary>
        public float StepTime => IsSwinging ? stepTime : 0f;

        /// <summary>次に押したときに出る段。</summary>
        public int NextStep => nextStep;

        /// <summary>次に振れるまでの残り（秒）。待っていなければ 0。</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>今の待ちの全体の長さ（秒）。残りの割合を出すのに使う。</summary>
        public float CooldownDuration { get; private set; }

        public bool IsCoolingDown => CooldownRemaining > 0f;

        /// <summary>seconds 秒待たせる。今の残りのほうが長ければそのまま（短い待ちで長い待ちを縮めない）。</summary>
        public void StartCooldown(float seconds)
        {
            if (seconds <= CooldownRemaining) return;

            CooldownRemaining = seconds;
            CooldownDuration = seconds;
        }

        /// <summary>待機に戻す（持ち替えたときなど）。</summary>
        public void Reset()
        {
            Step = -1;
            nextStep = 0;
            stepTime = 0f;
            graceRemaining = 0f;
            buffered = false;
            CooldownRemaining = 0f;
            CooldownDuration = 0f;
        }

        /// <summary>時間を deltaTime 秒進める。pressed はこのフレームに攻撃が押されたか。</summary>
        public ComboEvents Tick(float deltaTime, bool pressed, float speed = 1f)
        {
            if (deltaTime < 0f) deltaTime = 0f;
            if (speed <= 0f) speed = 1f;

            if (!IsSwinging)
            {
                if (IsCoolingDown)
                {
                    CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);
                    return ComboEvents.None;
                }

                if (pressed) return StartStep(nextStep);

                if (graceRemaining > 0f)
                {
                    graceRemaining -= deltaTime;
                    if (graceRemaining <= 0f)
                    {
                        graceRemaining = 0f;
                        return End();
                    }
                }

                return ComboEvents.None;
            }

            ComboStepTiming timing = steps[Step];
            float before = stepTime;
            stepTime += deltaTime * speed;

            if (pressed && stepTime >= timing.HitTime) buffered = true;

            var events = ComboEvents.None;
            if (before < timing.HitTime && stepTime >= timing.HitTime) events |= ComboEvents.Hit;
            else if (before == 0f && timing.HitTime == 0f && stepTime > 0f) events |= ComboEvents.Hit;

            if (stepTime < timing.Duration) return events;

            int following = Step + 1 >= steps.Length ? 0 : Step + 1;
            if (buffered && (following != 0 || cooldown <= 0f)) return events | StartStep(following);

            Step = -1;
            nextStep = following;
            stepTime = 0f;
            buffered = false;
            if (following == 0)
            {
                graceRemaining = 0f;
                return events | End();
            }

            graceRemaining = chainGrace;
            if (graceRemaining <= 0f) return events | End();

            return events;
        }

        /// <summary>コンボが切れて待機に戻る。次は 1 段目で、武器種の待ちに入る。</summary>
        private ComboEvents End()
        {
            nextStep = 0;
            StartCooldown(cooldown);
            return ComboEvents.Ended;
        }

        private ComboEvents StartStep(int step)
        {
            Step = step;
            nextStep = step + 1 >= steps.Length ? 0 : step + 1;
            stepTime = 0f;
            graceRemaining = 0f;
            buffered = false;
            return ComboEvents.StepStarted;
        }
    }
}
