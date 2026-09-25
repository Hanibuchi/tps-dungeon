using NUnit.Framework;
using UnityEngine;

namespace TpsDungeon.Interaction.Tests
{
    /// <summary>届く距離の判定と、プロンプトのキー表示と、情報欄を出すかどうかを確かめる。</summary>
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

        [Test]
        public void Details_ShownOnlyForTargetsThatProvideThem()
        {
            Assert.IsNotNull(InteractionPromptView.DetailsOf(new DetailedTarget()), "アイテムのように情報を持つものは出す");
            Assert.IsNull(InteractionPromptView.DetailsOf(new PlainTarget()), "ドアのように情報を持たないものは出さない");
            Assert.IsNull(InteractionPromptView.DetailsOf(null));
        }

        private class PlainTarget : IInteractable
        {
            public string PromptLabel => "開ける";
            public bool CanInteract(GameObject interactor) => true;
            public void Interact(GameObject interactor) { }
        }

        private sealed class DetailedTarget : PlainTarget, IInteractableDetails
        {
            public string DetailTitle => "回復薬";
            public string DetailBody => "説明";
            public Texture2D DetailIcon => null;
        }
    }
}
