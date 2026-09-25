using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.UiKit
{
    /// <summary>
    /// スライダーの溝に、値までを満たす帯（.rpg-slider__fill）を足す。
    /// 値が変わると帯の幅を書き換え、伸び縮みは USS のトランジションで動く。
    /// </summary>
    public static class SliderDecor
    {
        public const string FillUssClassName = "rpg-slider__fill";

        /// <summary>帯を足す。何度呼んでも 1 本だけ。</summary>
        public static void Attach(Slider slider)
        {
            if (slider == null) return;

            VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker == null || tracker.Q<VisualElement>(className: FillUssClassName) != null) return;

            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList(FillUssClassName);
            tracker.Add(fill);

            slider.RegisterValueChangedCallback(evt => SetFill(slider, fill, evt.newValue));
            SetFill(slider, fill, slider.value);
        }

        /// <summary>SetValueWithoutNotify で値を入れたあと、帯を合わせ直す。</summary>
        public static void Refresh(Slider slider)
        {
            VisualElement fill = slider?.Q<VisualElement>(className: FillUssClassName);
            if (fill != null) SetFill(slider, fill, slider.value);
        }

        private static void SetFill(Slider slider, VisualElement fill, float value)
        {
            float range = slider.highValue - slider.lowValue;
            float t = Mathf.Approximately(range, 0f) ? 0f : Mathf.Clamp01((value - slider.lowValue) / range);
            fill.style.width = Length.Percent(t * 100f);
        }
    }
}
