using System;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 効果音の波形をその場で作る（素材を使わない音）。UnityEngine に依存しない。モノラル、-1〜1 の float。
    ///
    ///   ComboThud … コンボの段が上がった合図。低い「ドン」。段が進むほど少しずつ高く、倍音と頭の雑音を足して硬くする
    ///   CriticalClang … クリティカルの「キィン」。金属の棒のような非整数倍音と、頭の短い雑音
    /// </summary>
    public static class ToneSynth
    {
        /// <summary>コンボの合図の 1 段目の高さ（Hz、G2）。</summary>
        public const float ComboBaseFrequency = 98f;

        /// <summary>コンボの合図が上がりきる段（ここから先は同じ音）。1 段で半音ずつ、10 半音上まで。</summary>
        public const int ComboTopStep = 11;

        public const float ComboThudDuration = 0.22f;
        public const float CriticalClangDuration = 0.4f;

        // 出来上がりの一番大きいところをこの大きさに揃える（割れないように少し下げる）。
        private const float Peak = 0.9f;

        /// <summary>コンボ count 段目（1 始まり）の合図の高さ（Hz）。1 段で半音ずつ上がり、ComboTopStep で止まる。</summary>
        public static float ComboFrequency(int count) => ComboBaseFrequency * (float)Math.Pow(2.0, ComboLevel(count) / 12.0);

        /// <summary>コンボ count 段目の合図。</summary>
        public static float[] ComboThud(int sampleRate, int count)
        {
            int level = ComboLevel(count);
            float frequency = ComboFrequency(count);
            var samples = new float[SampleCount(sampleRate, ComboThudDuration)];
            // (高さ, 大きさ, 減衰の速さ /秒)。上の 2 つは非整数倍で、段が進むほど大きくして硬くする。
            AddPartial(samples, sampleRate, frequency, 1f, 16f);
            AddPartial(samples, sampleRate, frequency * 2.4f, 0.2f + 0.04f * level, 30f);
            AddPartial(samples, sampleRate, frequency * 4.7f, 0.05f + 0.03f * level, 45f);
            AddNoise(samples, sampleRate, 0.35f + 0.04f * level, 0.012f);
            ApplyAttack(samples, sampleRate, 0.002f);
            Normalize(samples);
            return samples;
        }

        /// <summary>コンボ count 段目の、1 段目から上がった半音の数（0〜ComboTopStep - 1）。</summary>
        private static int ComboLevel(int count) => Math.Min(Math.Max(count, 1), ComboTopStep) - 1;

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
            // 打った瞬間の「カッ」。
            AddNoise(samples, sampleRate, 0.8f, 0.012f);

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

        /// <summary>
        /// 頭の seconds 秒に、amplitude から 0 へ消えていく雑音を足す（打った瞬間の「カッ」）。
        /// 少しだけ角を丸めて耳に痛くないようにする。毎回同じ音になるよう種は固定。
        /// </summary>
        private static void AddNoise(float[] samples, int sampleRate, float amplitude, float seconds)
        {
            var random = new Random(7);
            int length = Math.Min(samples.Length, Math.Max(1, (int)(sampleRate * seconds)));
            float previous = 0f;
            for (int i = 0; i < length; i++)
            {
                float x = (float)random.NextDouble() * 2f - 1f;
                previous = previous * 0.3f + x * 0.7f;
                samples[i] += previous * amplitude * (1f - (float)i / length);
            }
        }

        /// <summary>頭の seconds 秒を 0 から立ち上げる（出だしのプツッを消す）。終わりも短く絞る。</summary>
        private static void ApplyAttack(float[] samples, int sampleRate, float seconds)
        {
            int attack = Math.Min(samples.Length, Math.Max(1, (int)(sampleRate * seconds)));
            for (int i = 0; i < attack; i++) samples[i] *= (float)i / attack;

            int release = Math.Min(samples.Length, Math.Max(1, sampleRate / 100));
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
