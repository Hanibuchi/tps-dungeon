using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// ToneSynth で作った波形を AudioClip にして使い回す。最初に要ったときに作る。
    /// </summary>
    public static class SynthSounds
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<int, AudioClip> comboChimes = new Dictionary<int, AudioClip>();
        private static AudioClip criticalClang;

        /// <summary>コンボ count 段目（1 始まり）の合図。ToneSynth.ComboTopStep から先は同じ音。</summary>
        public static AudioClip ComboChime(int count)
        {
            int key = Mathf.Clamp(count, 1, ToneSynth.ComboTopStep);
            if (comboChimes.TryGetValue(key, out AudioClip clip) && clip != null) return clip;

            clip = Create($"Synth Combo {key}", ToneSynth.ComboChime(SampleRate, key));
            comboChimes[key] = clip;
            return clip;
        }

        /// <summary>クリティカルの「キィン」。</summary>
        public static AudioClip CriticalClang
        {
            get
            {
                if (criticalClang == null) criticalClang = Create("Synth Critical", ToneSynth.CriticalClang(SampleRate));
                return criticalClang;
            }
        }

        private static AudioClip Create(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
