using System;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 効果音の波形をその場で作る（素材を使わない音）。UnityEngine に依存しない。モノラル、-1〜1 の float。
    ///
    ///   ComboChime … コンボの段が上がった合図。倍音を少し重ねた短いベル
    ///   CriticalClang … クリティカルの「キィン」。金属の棒のような非整数倍音と、頭の短い雑音
    /// </summary>
    public static class ToneSynth
    {
        /// <summary>コンボの合図の 1 段目の高さ（Hz、C5）。</summary>
        public const float ComboBaseFrequency = 523.25f;

        /// <summary>コンボの合図が上がりきる段（ここから先は同じ高さ）。長音階で 2 オクターブ上。</summary>
        public const int ComboTopStep = 15;

        public const float ComboChimeDuration = 0.16f;
        public const float CriticalClangDuration = 0.4f;

        // 出来上がりの一番大きいところをこの大きさに揃える（割れないように少し下げる）。
        private const float Peak = 0.9f;

        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };

        /// <summary>コンボ count 段目（1 始まり）の合図の高さ（Hz）。長音階で 1 段ずつ上がり、ComboTopStep で止まる。</summary>
        public static float ComboFrequency(int count)
        {
            int k = Math.Min(Math.Max(count, 1), ComboTopStep) - 1;
            int semitones = 12 * (k / MajorScale.Length) + MajorScale[k % MajorScale.Length];
            return ComboBaseFrequency * (float)Math.Pow(2.0, semitones / 12.0);
        }

        /// <summary>コンボ count 段目の合図。</summary>
        public static float[] ComboChime(int sampleRate, int count)
        {
            float frequency = ComboFrequency(count);
            var samples = new float[SampleCount(sampleRate, ComboChimeDuration)];
            // (倍音の倍率, 大きさ, 減衰の速さ /秒)
            AddPartial(samples, sampleRate, frequency, 1f, 18f);
            AddPartial(samples, sampleRate, frequency * 2f, 0.35f, 26f);
            AddPartial(samples, sampleRate, frequency * 3f, 0.12f, 34f);
            ApplyAttack(samples, sampleRate, 0.003f);
            Normalize(samples);
            return samples;
        }

        /// <summary>クリティカルの「キィン」。</summary>
        public static float[] CriticalClang(int sampleRate)
        {
            const float fundamental = 1480f;
            var samples = new float[SampleCount(sampleRate, CriticalClangDuration)];
            // 両端の自由な棒の倍音の比（1 : 2.76 : 5.40 : 8.93）。高い倍音ほど早く消える。
            AddPartial(samples, sampleRate, fundamental, 1f, 9f);
            AddPartial(samples, sampleRate, fundamental * 2.76f, 0.6f, 14f);
            AddPartial(samples, sampleRate, fundamental * 5.40f, 0.35f, 22f);
            AddPartial(samples, sampleRate, fundamental * 8.93f, 0.2f, 32f);
            // 打った瞬間の「カッ」。毎回同じ音になるよう種を固定する。
            var random = new Random(7);
            int noiseLength = Math.Min(samples.Length, (int)(sampleRate * 0.012f));
            for (int i = 0; i < noiseLength; i++)
            {
                float fade = 1f - (float)i / noiseLength;
                samples[i] += ((float)random.NextDouble() * 2f - 1f) * 0.8f * fade;
            }

            ApplyAttack(samples, sampleRate, 0.001f);
            Normalize(samples);
            return samples;
        }

        private static int SampleCount(int sampleRate, float seconds) => Math.Max(1, (int)(Math.Max(1, sampleRate) * seconds));

        private static void AddPartial(float[] samples, int sampleRate, float frequency, float amplitude, float decay)
        {
            double step = 2.0 * Math.PI * frequency / sampleRate;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / sampleRate;
                samples[i] += (float)(amplitude * Math.Exp(-decay * t) * Math.Sin(step * i));
            }
        }

        /// <summary>頭の seconds 秒を 0 から立ち上げる（出だしのプツッを消す）。終わりも短く絞る。</summary>
        private static void ApplyAttack(float[] samples, int sampleRate, float seconds)
        {
            int attack = Math.Min(samples.Length, Math.Max(1, (int)(sampleRate * seconds)));
            for (int i = 0; i < attack; i++) samples[i] *= (float)i / attack;

            int release = Math.Min(samples.Length, Math.Max(1, sampleRate / 200));
            for (int i = 0; i < release; i++) samples[samples.Length - 1 - i] *= (float)i / release;
        }

        private static void Normalize(float[] samples)
        {
            float max = 0f;
            foreach (float s in samples) max = Math.Max(max, Math.Abs(s));
            if (max <= 0f) return;

            float scale = Peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
        }
    }
}
