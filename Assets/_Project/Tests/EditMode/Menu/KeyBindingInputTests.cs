using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Menu.Tests
{
    /// <summary>割り当てたキーを実際に押して、アクションが反応するかを確かめる。</summary>
    public class KeyBindingInputTests : InputTestFixture
    {
        private const string ActionsPath = "Assets/_Project/Settings/Input/PlayerControls.inputactions";

        private InputActionAsset actions;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            actions = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(actions);
            base.TearDown();
        }

        [Test]
        public void 二つの枠のどちらのキーでも反応し_空にした枠は反応しない()
        {
            InputAction jump = actions.FindAction("Jump", true);
            int first = KeyBindings.FindBindingIndex(jump, null, 0);
            int second = KeyBindings.FindBindingIndex(jump, null, 1);
            KeyBindings.Assign(jump, second, "<Keyboard>/j");
            jump.Enable();

            Press(keyboard.jKey);
            Assert.That(jump.triggered, Is.True, "2 つ目の枠");
            Release(keyboard.jKey);

            Press(keyboard.spaceKey);
            Assert.That(jump.triggered, Is.True, "1 つ目の枠");
            Release(keyboard.spaceKey);

            jump.Disable();
            KeyBindings.Clear(jump, first);
            jump.Enable();

            Press(keyboard.spaceKey);
            Assert.That(jump.triggered, Is.False, "空にした枠");
            Release(keyboard.spaceKey);
        }

        [Test]
        public void 移動の2つ目の枠に割り当てたキーで動ける()
        {
            InputAction move = actions.FindAction("Move", true);
            KeyBindings.Assign(move, KeyBindings.FindBindingIndex(move, "left", 1), "<Keyboard>/leftArrow");
            move.Enable();

            Press(keyboard.leftArrowKey);
            Assert.That(move.ReadValue<Vector2>().x, Is.EqualTo(-1f).Within(1e-4f));
            Release(keyboard.leftArrowKey);

            Press(keyboard.aKey);
            Assert.That(move.ReadValue<Vector2>().x, Is.EqualTo(-1f).Within(1e-4f), "既定の A もそのまま効く");
            Release(keyboard.aKey);
        }
    }
}
