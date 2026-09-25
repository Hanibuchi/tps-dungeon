using System;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Audio.UI
{
    /// <summary>
    /// 全体 / BGM / SE のスライダーと GameAudio の音量をつなぐ部品。
    /// AudioVolumeRows.uxml の行を含む要素を渡せば、単体の設定パネルでもポーズ画面の設定でも同じように動く。
    /// 使い終わったら Dispose して音量変更の購読を外すこと。
    /// </summary>
    public sealed class AudioVolumeSection : IDisposable
    {
        private readonly Slider[] sliders = new Slider[3];
        private readonly Label[] values = new Label[3];
        private readonly AudioClip previewClip;
        private AudioVolumeController subscribed;
        private float nextPreviewTime;

        public AudioVolumeSection(VisualElement root, AudioClip previewClip)
        {
            this.previewClip = previewClip;

            Bind(root, AudioChannel.Master, "master-volume", "master-value");
            Bind(root, AudioChannel.Bgm, "bgm-volume", "bgm-value");
            Bind(root, AudioChannel.Se, "se-volume", "se-value");

            subscribed = GameAudio.Instance != null ? GameAudio.Instance.Volumes : null;
            if (subscribed != null) subscribed.VolumeChanged += OnVolumeChanged;

            Pull();
        }

        public void Dispose()
        {
            if (subscribed != null) subscribed.VolumeChanged -= OnVolumeChanged;
            subscribed = null;
        }

        /// <summary>設定アセットの既定値に戻す。</summary>
        public void ResetToDefaults()
        {
            GameAudio.Instance?.Volumes?.ResetToDefaults();
            Pull();
        }

        /// <summary>今の音量をスライダーに反映する。</summary>
        public void Pull()
        {
            AudioVolumeController volumes = GameAudio.Instance?.Volumes;
            if (volumes == null) return;

            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                float value = volumes.GetVolume(channel);
                Slider slider = sliders[(int)channel];
                if (slider != null)
                {
                    slider.SetValueWithoutNotify(value);
                    SliderDecor.Refresh(slider);
                }

                Show(channel, value);
            }
        }

        private void Bind(VisualElement root, AudioChannel channel, string sliderName, string valueName)
        {
            var slider = root.Q<Slider>(sliderName);
            sliders[(int)channel] = slider;
            values[(int)channel] = root.Q<Label>(valueName);
            if (slider == null) return;

            SliderDecor.Attach(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                GameAudio.Instance?.Volumes?.SetVolume(channel, evt.newValue);
                Show(channel, evt.newValue);
                UiTransitions.Flash(values[(int)channel], "is-flash", 90);
                if (channel == AudioChannel.Se) PlayPreview();
            });
        }

        private void OnVolumeChanged(AudioChannel channel, float value)
        {
            Slider slider = sliders[(int)channel];
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
                SliderDecor.Refresh(slider);
            }

            Show(channel, value);
        }

        private void Show(AudioChannel channel, float value)
        {
            Label label = values[(int)channel];
            if (label != null) label.text = Mathf.RoundToInt(value * 100f).ToString();
        }

        /// <summary>SE を触っている間、音量が分かるように短い音を鳴らす。鳴らしすぎないよう間引く。</summary>
        private void PlayPreview()
        {
            if (previewClip == null || Time.unscaledTime < nextPreviewTime) return;

            nextPreviewTime = Time.unscaledTime + 0.12f;
            GameAudio.Instance?.PlaySe(previewClip);
        }
    }
}
