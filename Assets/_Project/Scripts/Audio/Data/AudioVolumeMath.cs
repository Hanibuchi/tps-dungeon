using System;

namespace TpsDungeon.Audio.Data
{
    /// <summary>
    /// スライダーの 0〜1 と AudioMixer の dB を相互変換する。
    /// UnityEngine に依存しないので、エディタを開いたままでも素の C# として検証できる。
    /// </summary>
    public static class AudioVolumeMath
    {
        /// <summary>無音として扱う dB。AudioMixer の Attenuation の下限に合わせてある。</summary>
        public const float MinDecibels = -80f;

        /// <summary>これ以下の音量は無音とみなす。log10(0) を踏まないための下限でもある。</summary>
        public const float MinAudibleNormalized = 0.0001f;

        /// <summary>0〜1 に丸める。NaN は 0 として扱う。</summary>
        public static float ClampNormalized(float normalized)
        {
            if (float.IsNaN(normalized)) return 0f;
            if (normalized < 0f) return 0f;
            return normalized > 1f ? 1f : normalized;
        }

        /// <summary>-80〜0 dB に丸める。NaN は無音として扱う。</summary>
        public static float ClampDecibels(float decibels)
        {
            if (float.IsNaN(decibels)) return MinDecibels;
            if (decibels < MinDecibels) return MinDecibels;
            return decibels > 0f ? 0f : decibels;
        }

        /// <summary>
        /// 0〜1 の音量を dB に変換する。1 が 0dB、0.5 が約 -6dB、0 が -80dB(無音)。
        /// 人間の音量感覚は対数的なので、スライダーの値をそのまま dB にはできない。
        /// </summary>
        public static float ToDecibels(float normalized)
        {
            float value = ClampNormalized(normalized);
            if (value <= MinAudibleNormalized) return MinDecibels;
            return ClampDecibels((float)(Math.Log10(value) * 20.0));
        }

        /// <summary>dB を 0〜1 の音量に戻す。<see cref="ToDecibels"/> の逆変換。</summary>
        public static float FromDecibels(float decibels)
        {
            float value = ClampDecibels(decibels);
            if (value <= MinDecibels) return 0f;
            return ClampNormalized((float)Math.Pow(10.0, value / 20.0));
        }
    }
}
