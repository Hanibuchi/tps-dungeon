using NUnit.Framework;

namespace TpsDungeon.Combat.Tests
{
    /// <summary>生成する効果音（コンボの合図・クリティカル）の波形を確かめる。</summary>
    public sealed class ToneSynthTests
    {
        private const int Rate = 44100;

        [Test]
        public void コンボの合図は段が進むほど高く_上がりきったら止まる()
        {
            Assert.AreEqual(ToneSynth.ComboBaseFrequency, ToneSynth.ComboFrequency(1), 1e-3f);
            for (int i = 1; i < ToneSynth.ComboTopStep; i++)
                Assert.Greater(ToneSynth.ComboFrequency(i + 1), ToneSynth.ComboFrequency(i));

            // 1 段で半音ずつ、10 半音上で止まる。
            Assert.AreEqual(ToneSynth.ComboBaseFrequency * (float)System.Math.Pow(2.0, 1.0 / 12.0), ToneSynth.ComboFrequency(2), 1e-3f);
            float top = ToneSynth.ComboFrequency(ToneSynth.ComboTopStep);
            Assert.AreEqual(ToneSynth.ComboBaseFrequency * (float)System.Math.Pow(2.0, 10.0 / 12.0), top, 0.01f);
            Assert.AreEqual(top, ToneSynth.ComboFrequency(100), 1e-3f);
            Assert.AreEqual(ToneSynth.ComboFrequency(1), ToneSynth.ComboFrequency(0), 1e-3f);
        }

        [Test]
        public void 長さのとおりで_割れず_無音でもない()
        {
            AssertWave(ToneSynth.ComboThud(Rate, 3), ToneSynth.ComboThudDuration);
            AssertWave(ToneSynth.CriticalClang(Rate), ToneSynth.CriticalClangDuration);
        }

        [Test]
        public void クリティカルの音は毎回同じ()
        {
            CollectionAssert.AreEqual(ToneSynth.CriticalClang(Rate), ToneSynth.CriticalClang(Rate));
        }

        private static void AssertWave(float[] samples, float seconds)
        {
            Assert.AreEqual((int)(Rate * seconds), samples.Length);
            float max = 0f;
            foreach (float s in samples) max = System.Math.Max(max, System.Math.Abs(s));
            Assert.LessOrEqual(max, 1f);
            Assert.Greater(max, 0.5f);
            Assert.AreEqual(0f, samples[0], 1e-4f, "出だしは 0 から");
            Assert.AreEqual(0f, samples[samples.Length - 1], 1e-4f, "終わりも 0 へ");
        }
    }
}
