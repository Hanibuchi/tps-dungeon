using NUnit.Framework;
using TpsDungeon.Audio.Data;

namespace TpsDungeon.Audio.Tests
{
    /// <summary>
    /// スライダーの 0〜1 とミキサーの dB の変換を確かめる。
    /// ここが狂うと「スライダーは真ん中なのに爆音」のような形で効いてくる。
    /// </summary>
    public sealed class AudioVolumeMathTests
    {
        [Test]
        public void FullVolume_IsZeroDecibels()
        {
            Assert.AreEqual(0f, AudioVolumeMath.ToDecibels(1f), 0.001f, "音量 1 は 0dB でなければならない");
        }

        [Test]
        public void Silence_IsMinimumDecibels()
        {
            Assert.AreEqual(AudioVolumeMath.MinDecibels, AudioVolumeMath.ToDecibels(0f), 0.001f,
                "音量 0 は無音(-80dB)でなければならない");
        }

        [Test]
        public void HalfVolume_IsAboutMinusSixDecibels()
        {
            Assert.AreEqual(-6.0206f, AudioVolumeMath.ToDecibels(0.5f), 0.001f,
                "音量を半分にすると約 -6dB になる");
        }

        [Test]
        public void ToDecibels_NeverExceedsZero()
        {
            Assert.AreEqual(0f, AudioVolumeMath.ToDecibels(4f), 0.001f, "1 を超える入力は 0dB に丸める");
        }

        [Test]
        public void ToDecibels_TreatsNaNAsSilence()
        {
            Assert.AreEqual(AudioVolumeMath.MinDecibels, AudioVolumeMath.ToDecibels(float.NaN), 0.001f);
        }

        [TestCase(0.01f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        public void RoundTrip_ReturnsTheSameVolume(float normalized)
        {
            float roundTripped = AudioVolumeMath.FromDecibels(AudioVolumeMath.ToDecibels(normalized));
            Assert.AreEqual(normalized, roundTripped, 0.0005f, "dB に直して戻すと元の音量に戻る");
        }

        [Test]
        public void FromDecibels_MapsSilenceToZero()
        {
            Assert.AreEqual(0f, AudioVolumeMath.FromDecibels(AudioVolumeMath.MinDecibels), 0.0001f);
            Assert.AreEqual(0f, AudioVolumeMath.FromDecibels(-200f), 0.0001f, "下限より下も無音として扱う");
        }

        [Test]
        public void ClampNormalized_KeepsValuesInRange()
        {
            Assert.AreEqual(0f, AudioVolumeMath.ClampNormalized(-3f), 0.0001f);
            Assert.AreEqual(1f, AudioVolumeMath.ClampNormalized(3f), 0.0001f);
            Assert.AreEqual(0.4f, AudioVolumeMath.ClampNormalized(0.4f), 0.0001f);
        }

        [Test]
        public void ClampDecibels_KeepsValuesInRange()
        {
            Assert.AreEqual(AudioVolumeMath.MinDecibels, AudioVolumeMath.ClampDecibels(-500f), 0.0001f);
            Assert.AreEqual(0f, AudioVolumeMath.ClampDecibels(12f), 0.0001f);
        }
    }
}
