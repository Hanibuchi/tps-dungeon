using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// USS のトランジションで開け閉めするための小道具。
    ///
    /// display:none はアニメーションできないので、開閉は次の手順に揃える。
    /// - 開く: display を flex にしてから、1 拍おいて <see cref="ShownClass"/> を付ける（付いた状態が「見えている」姿）。
    /// - 閉じる: <see cref="ShownClass"/> を外し、要素に書かれたトランジションが終わる頃に display を none にする。
    /// 見た目（どこから現れてどう消えるか）はすべて USS 側に書く。
    ///
    /// UI Toolkit の時計は実時間なので、timeScale が 0 のポーズ中でも動く。
    /// </summary>
    public static class UiTransitions
    {
        /// <summary>見えている状態を表すクラス。</summary>
        public const string ShownClass = "is-shown";

        // 閉じる途中で開き直されたときに、古い「display を none にする」予約を無効にするための通し番号。
        private sealed class State
        {
            public int Version;
            public IVisualElementScheduledItem Pulse;
        }

        private static readonly ConditionalWeakTable<VisualElement, State> States = new ConditionalWeakTable<VisualElement, State>();

        /// <summary>見えているか（閉じる途中は含まない）。</summary>
        public static bool IsShown(VisualElement element) => element != null && element.ClassListContains(ShownClass);

        /// <summary>開く。既に開いていれば何もしない。</summary>
        public static void Show(VisualElement element)
        {
            if (element == null) return;

            State state = States.GetOrCreateValue(element);
            int version = ++state.Version;
            element.style.display = DisplayStyle.Flex;

            // display を戻したのと同じ更新でクラスを付けると、閉じた姿を経ずに一瞬で開いてしまう。
            // 1 拍おいて、閉じた姿のスタイルが一度解決されてから付ける。
            element.schedule.Execute(() =>
            {
                if (state.Version == version) element.AddToClassList(ShownClass);
            }).StartingIn(1);
        }

        /// <summary>閉じる。トランジションが終わってから display を none にし、onHidden を呼ぶ。</summary>
        public static void Hide(VisualElement element, System.Action onHidden = null)
        {
            if (element == null) return;

            State state = States.GetOrCreateValue(element);
            int version = ++state.Version;
            element.RemoveFromClassList(ShownClass);

            if (element.resolvedStyle.display == DisplayStyle.None)
            {
                onHidden?.Invoke();
                return;
            }

            element.schedule.Execute(() =>
            {
                if (state.Version != version) return;
                element.style.display = DisplayStyle.None;
                onHidden?.Invoke();
            }).StartingIn(LongestTransitionMs(element) + 20);
        }

        /// <summary>アニメーションなしで即座に閉じた状態にする。初期化用。</summary>
        public static void HideImmediately(VisualElement element)
        {
            if (element == null) return;

            States.GetOrCreateValue(element).Version++;
            element.RemoveFromClassList(ShownClass);
            element.style.display = DisplayStyle.None;
        }

        /// <summary>開閉を切り替える。</summary>
        public static void SetShown(VisualElement element, bool shown)
        {
            if (shown) Show(element);
            else Hide(element);
        }

        /// <summary>
        /// 子要素のトランジションの開始を stepMs ずつずらす（一覧が 1 行ずつ現れる）。
        /// 閉じるときまで遅れると間延びするので、閉じる前に <see cref="ClearStagger"/> で戻すこと。
        /// </summary>
        public static void Stagger(VisualElement parent, long stepMs, long startMs = 0)
        {
            if (parent == null) return;

            int i = 0;
            foreach (VisualElement child in parent.Children())
            {
                child.style.transitionDelay = new List<TimeValue> { new TimeValue(startMs + stepMs * i, TimeUnit.Millisecond) };
                i++;
            }
        }

        /// <summary><see cref="Stagger"/> でずらした開始を USS の指定に戻す。</summary>
        public static void ClearStagger(VisualElement parent)
        {
            if (parent == null) return;
            foreach (VisualElement child in parent.Children()) child.style.transitionDelay = StyleKeyword.Null;
        }

        /// <summary>
        /// className を periodMs ごとに付け外しして明滅させる。USS のトランジションはループしないため。
        /// 止めるときは <see cref="StopPulse"/>。
        /// </summary>
        public static void Pulse(VisualElement element, string className, long periodMs)
        {
            if (element == null) return;

            State state = States.GetOrCreateValue(element);
            state.Pulse?.Pause();
            state.Pulse = element.schedule.Execute(() => element.ToggleInClassList(className)).Every(periodMs);
        }

        public static void StopPulse(VisualElement element, string className)
        {
            if (element == null) return;

            if (States.TryGetValue(element, out State state))
            {
                state.Pulse?.Pause();
                state.Pulse = null;
            }

            element.RemoveFromClassList(className);
        }

        /// <summary>className を一瞬だけ付ける。決まった瞬間に光らせるなど。</summary>
        public static void Flash(VisualElement element, string className, long durationMs)
        {
            if (element == null) return;

            element.RemoveFromClassList(className);
            element.AddToClassList(className);
            element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(durationMs);
        }

        /// <summary>要素に書かれたトランジションのうち、一番遅く終わるものの時間（開始の遅れ込み）。</summary>
        public static long LongestTransitionMs(VisualElement element)
        {
            float longest = 0f;
            var durations = new List<TimeValue>(element.resolvedStyle.transitionDuration);
            var delays = new List<TimeValue>(element.resolvedStyle.transitionDelay);
            for (int i = 0; i < durations.Count; i++)
            {
                float delay = delays.Count > 0 ? ToMs(delays[i % delays.Count]) : 0f;
                longest = System.Math.Max(longest, ToMs(durations[i]) + delay);
            }

            return (long)longest;
        }

        private static float ToMs(TimeValue value) => value.unit == TimeUnit.Second ? value.value * 1000f : value.value;
    }
}
