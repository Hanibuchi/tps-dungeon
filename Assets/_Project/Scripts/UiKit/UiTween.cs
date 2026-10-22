using System;
using System.Diagnostics;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// USS のトランジションでは書けない動き（数を回す・曲線に沿って飛ばす・線を伸ばす）を、要素の schedule で毎フレーム進める小道具。
    /// 時間は実時間なので、timeScale が 0 のポーズ中でも動く。返した <see cref="Handle"/> の Stop で途中で止められる（終わりの処理は呼ばない）。
    /// </summary>
    public static class UiTween
    {
        public sealed class Handle
        {
            internal IVisualElementScheduledItem Item;
            internal Action Complete;

            public bool IsRunning { get; internal set; }

            /// <summary>止める。終わりの処理は呼ばない。</summary>
            public void Stop()
            {
                IsRunning = false;
                Item?.Pause();
            }

            /// <summary>最後まで飛ばす（step に 1 を渡し、終わりの処理を呼ぶ）。</summary>
            public void Finish()
            {
                if (!IsRunning) return;
                Stop();
                Complete?.Invoke();
            }
        }

        /// <summary>
        /// durationMs かけて step に 0→1 を渡す（easing を通した値）。delayMs だけ待ってから始める。終わったら onComplete。
        /// </summary>
        public static Handle Run(VisualElement owner, long durationMs, Action<float> step, Action onComplete = null,
            Func<float, float> easing = null, long delayMs = 0)
        {
            var handle = new Handle { IsRunning = true };
            if (owner == null) return handle;

            easing ??= EaseOutCubic;
            var clock = new Stopwatch();
            bool started = false;
            handle.Complete = () =>
            {
                step?.Invoke(easing(1f));
                onComplete?.Invoke();
            };

            handle.Item = owner.schedule.Execute(() =>
            {
                if (!handle.IsRunning) return;
                if (!started)
                {
                    started = true;
                    clock.Start();
                }

                float t = durationMs <= 0 ? 1f : Math.Min(1f, clock.ElapsedMilliseconds / (float)durationMs);
                if (t < 1f)
                {
                    step?.Invoke(easing(t));
                    return;
                }

                handle.IsRunning = false;
                handle.Item.Pause();
                handle.Complete();
            }).StartingIn(delayMs).Every(16);
            return handle;
        }

        /// <summary>delayMs のあと 1 回だけ action を呼ぶ。</summary>
        public static Handle After(VisualElement owner, long delayMs, Action action) => Run(owner, 0, null, action, null, delayMs);

        public static float Linear(float t) => t;

        public static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseInCubic(float t) => t * t * t;

        public static float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;

        /// <summary>少し行き過ぎて戻る。</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
