using NUnit.Framework;
using TpsDungeon.Audio.DebugTools;

namespace TpsDungeon.Audio.Tests
{
    /// <summary>
    /// デバッグ表示が出す距離減衰の割合を確かめる。
    /// ここが AudioSource の Linear rolloff とずれると、
    /// 「画面は 50% と出ているのに耳では無音」のような形で確認作業そのものが信用できなくなる。
    /// </summary>
    public sealed class AudioDebugViewTests
    {
        private const float MinDistance = 1f;
        private const float MaxDistance = 30f;

        [Test]
        public void LinearRolloff_AtMinDistance_IsFullVolume()
        {
            Assert.AreEqual(1f, AudioDebugView.LinearRolloffFactor(MinDistance, MinDistance, MaxDistance), 0.0001f,
                "minDistance までは減衰しない");
        }

        [Test]
        public void LinearRolloff_AtMaxDistance_IsSilent()
        {
            Assert.AreEqual(0f, AudioDebugView.LinearRolloffFactor(MaxDistance, MinDistance, MaxDistance), 0.0001f,
                "maxDistance で無音になる");
        }

        [Test]
        public void LinearRolloff_Halfway_IsAboutHalf()
        {
            float halfway = (MinDistance + MaxDistance) * 0.5f;
            Assert.AreEqual(0.5f, AudioDebugView.LinearRolloffFactor(halfway, MinDistance, MaxDistance), 0.0001f,
                "min と max のちょうど中間で半分になる");
        }

        [Test]
        public void LinearRolloff_ClampsOutOfRange()
        {
            Assert.AreEqual(1f, AudioDebugView.LinearRolloffFactor(-5f, MinDistance, MaxDistance), 0.0001f,
                "minDistance より近い距離は 1 に丸める");
            Assert.AreEqual(0f, AudioDebugView.LinearRolloffFactor(100f, MinDistance, MaxDistance), 0.0001f,
                "maxDistance より遠い距離は 0 に丸める");
        }

        [Test]
        public void LinearRolloff_DegenerateRange_DoesNotDivideByZero()
        {
            Assert.AreEqual(1f, AudioDebugView.LinearRolloffFactor(5f, 10f, 10f), 0.0001f,
                "min と max が同じなら、min 以内は鳴る");
            Assert.AreEqual(0f, AudioDebugView.LinearRolloffFactor(15f, 10f, 10f), 0.0001f,
                "min と max が同じなら、min を超えたら無音");
        }
    }
}
