using System;
using System.Collections.Generic;
using TpsDungeon.Audio.Authoring;
using TpsDungeon.Audio.Data;
using UnityEngine;

namespace TpsDungeon.Audio.Runtime
{
    /// <summary>
    /// 全体 / BGM / SE の音量を持つ唯一の場所。
    /// 0〜1 の値を持ち、ミキサーへは dB に直して流し、同時に保存する。
    /// </summary>
    public sealed class AudioVolumeController
    {
        private readonly GameAudioConfig config;
        private readonly Dictionary<AudioChannel, float> volumes = new Dictionary<AudioChannel, float>();

        /// <summary>音量が変わったときに飛ぶ。UI のラベル更新などに使う。</summary>
        public event Action<AudioChannel, float> VolumeChanged;

        public AudioVolumeController(GameAudioConfig config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));

            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                volumes[channel] = AudioSettingsStore.Load(channel, config.GetDefaultVolume(channel));
            }

            ApplyAll();
        }

        /// <summary>今の音量(0〜1)。</summary>
        public float GetVolume(AudioChannel channel)
        {
            return volumes.TryGetValue(channel, out float value) ? value : config.GetDefaultVolume(channel);
        }

        /// <summary>音量(0〜1)を変える。ミキサーへの反映と保存までここで済ませる。</summary>
        public void SetVolume(AudioChannel channel, float normalized)
        {
            float value = AudioVolumeMath.ClampNormalized(normalized);
            if (volumes.TryGetValue(channel, out float current) && Mathf.Approximately(current, value)) return;

            volumes[channel] = value;
            Apply(channel, value);
            AudioSettingsStore.Save(channel, value);
            VolumeChanged?.Invoke(channel, value);
        }

        /// <summary>設定アセットの既定値に戻す。</summary>
        public void ResetToDefaults()
        {
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                SetVolume(channel, config.GetDefaultVolume(channel));
            }
        }

        /// <summary>保存をディスクに書き出す。アプリ終了時や設定画面を閉じたときに呼ぶ。</summary>
        public void Flush()
        {
            AudioSettingsStore.Flush();
        }

        /// <summary>持っている値をまとめてミキサーへ流し直す。</summary>
        public void ApplyAll()
        {
            foreach (KeyValuePair<AudioChannel, float> entry in volumes)
            {
                Apply(entry.Key, entry.Value);
            }
        }

        private void Apply(AudioChannel channel, float normalized)
        {
            if (config.Mixer == null) return;

            // SetFloat は再生中でないと失敗する。エディタの編集中に呼ばれても害は無いので黙って通す。
            config.Mixer.SetFloat(config.GetVolumeParameter(channel), AudioVolumeMath.ToDecibels(normalized));
        }
    }
}
