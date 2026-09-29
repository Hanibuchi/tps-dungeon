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
    ///
    /// 時間は speed（攻撃速度の倍率）を掛けて進める。猶予は実時間のまま。
    /// </summary>
    public sealed class MeleeComboState
    {
        private readonly ComboStepTiming[] steps;
        private readonly float chainGrace;

        private float stepTime;
        private float graceRemaining;
        private bool buffered;
        private int nextStep;

        public MeleeComboState(IReadOnlyList<ComboStepTiming> steps, float chainGrace)
        {
            if (steps == null || steps.Count == 0) throw new ArgumentException("段が 1 つも無い", nameof(steps));

            this.steps = new ComboStepTiming[steps.Count];
            for (int i = 0; i < steps.Count; i++) this.steps[i] = steps[i];
            this.chainGrace = Math.Max(0f, chainGrace);
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

        /// <summary>待機に戻す（持ち替えたときなど）。</summary>
        public void Reset()
        {
            Step = -1;
            nextStep = 0;
            stepTime = 0f;
            graceRemaining = 0f;
            buffered = false;
        }

        /// <summary>時間を deltaTime 秒進める。pressed はこのフレームに攻撃が押されたか。</summary>
        public ComboEvents Tick(float deltaTime, bool pressed, float speed = 1f)
        {
            if (deltaTime < 0f) deltaTime = 0f;
            if (speed <= 0f) speed = 1f;

            if (!IsSwinging)
            {
                if (pressed) return StartStep(nextStep);

                if (graceRemaining > 0f)
                {
                    graceRemaining -= deltaTime;
                    if (graceRemaining <= 0f)
                    {
                        graceRemaining = 0f;
                        nextStep = 0;
                        return ComboEvents.Ended;
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
            if (buffered) return events | StartStep(following);

            Step = -1;
            nextStep = following;
            stepTime = 0f;
            if (following == 0)
            {
                graceRemaining = 0f;
                return events | ComboEvents.Ended;
            }

            graceRemaining = chainGrace;
            if (graceRemaining <= 0f)
            {
                nextStep = 0;
                return events | ComboEvents.Ended;
            }

            return events;
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
