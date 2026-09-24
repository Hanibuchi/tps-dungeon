using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Interaction.Tests
{
    /// <summary>届く距離の判定と、プロンプトのキー表示を確かめる。</summary>
    public sealed class InteractionTests
    {
        [Test]
        public void Reach_InsideRange_IsReachable()
        {
            Assert.IsTrue(PlayerInteractor.IsWithinReach(Vector3.zero, new Vector3(1f, 1f, 1f), 2.5f));
        }

        [Test]
        public void Reach_ExactlyAtRange_IsReachable()
        {
            Assert.IsTrue(PlayerInteractor.IsWithinReach(Vector3.zero, new Vector3(0f, 0f, 2.5f), 2.5f));
        }

        [Test]
        public void Reach_BeyondRange_IsNotReachable()
        {
            Assert.IsFalse(PlayerInteractor.IsWithinReach(Vector3.zero, new Vector3(0f, 0f, 2.51f), 2.5f),
                "照準が遠くのドアに合っていても、キャラが近づくまでは触れない");
        }

        [TestCase("E", "E")]
        [TestCase(" E ", "E")]
        [TestCase("", "?")]
        [TestCase(null, "?")]
        public void KeyCap_ShowsBindingOrPlaceholder(string display, string expected)
        {
            Assert.AreEqual(expected, InteractionPromptView.KeyCapText(display));
        }
    }
}
