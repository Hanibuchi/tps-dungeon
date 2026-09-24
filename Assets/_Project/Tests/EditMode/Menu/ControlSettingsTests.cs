using System.IO;
using NUnit.Framework;
using TpsDungeon.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Menu.Tests
{
    /// <summary>キー設定と視点の設定を、実際の入力アセット（PlayerControls）の写しに当てて確かめる。</summary>
    public class ControlSettingsTests
    {
        private const string ActionsPath = "Assets/_Project/Settings/Input/PlayerControls.inputactions";

        private InputActionAsset actions;

        [SetUp]
        public void SetUp()
        {
            actions = LoadActions();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actions);
        }

        private static InputActionAsset LoadActions()
        {
            return InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
        }

        private InputAction Action(KeyBindingEntry entry) => actions.FindAction(entry.ActionName, true);

        private int Index(KeyBindingEntry entry, int slot) => KeyBindings.FindBindingIndex(Action(entry), entry.Part, slot);

        private static KeyBindingEntry Entry(string actionName, string part = null)
        {
            foreach (KeyBindingEntry entry in KeyBindings.Entries)
            {
                if (entry.ActionName == actionName && entry.Part == part) return entry;
            }

            Assert.Fail($"キー設定の行が無い: {actionName} {part}");
            return default;
        }

        // ---- 枠 ------------------------------------------------------------

        [Test]
        public void 全ての行に枠が2つあり_既定では1つ目だけ埋まっている()
        {
            foreach (KeyBindingEntry entry in KeyBindings.Entries)
            {
                InputAction action = Action(entry);
                int first = Index(entry, 0);
                int second = Index(entry, 1);

                Assert.That(first, Is.GreaterThanOrEqualTo(0), entry.Label + " の 1 つ目の枠");
                Assert.That(second, Is.GreaterThanOrEqualTo(0), entry.Label + " の 2 つ目の枠");
                Assert.That(Index(entry, 2), Is.EqualTo(-1), entry.Label + " に 3 つ目の枠は無い");
                Assert.That(action.bindings[first].path, Is.Not.Empty, entry.Label);
                Assert.That(action.bindings[second].path, Is.Empty, entry.Label);
                Assert.That(KeyBindings.Display(action, second), Is.EqualTo(KeyBindings.EmptyDisplay), entry.Label);
            }
        }

        [Test]
        public void 移動の枠は上下左右の部品を指す()
        {
            InputAction move = actions.FindAction("Move", true);
            string[] parts = { "up", "down", "left", "right" };
            string[] defaults = { "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d" };

            for (int i = 0; i < parts.Length; i++)
            {
                KeyBindingEntry entry = Entry("Move", parts[i]);
                InputBinding first = move.bindings[Index(entry, 0)];
                InputBinding second = move.bindings[Index(entry, 1)];

                Assert.That(first.isPartOfComposite && first.name == parts[i], Is.True);
                Assert.That(first.path, Is.EqualTo(defaults[i]));
                Assert.That(second.isPartOfComposite && second.name == parts[i], Is.True);
                Assert.That(Index(entry, 1), Is.Not.EqualTo(Index(entry, 0)));
            }
        }

        [Test]
        public void インタラクトの案内には空き枠を出さず_割り当てたキーは全部出す()
        {
            KeyBindingEntry entry = Entry("Interact");
            InputAction interact = Action(entry);

            Assert.That(PlayerInteractor.BindingDisplay(interact, KeyBindings.Group), Is.EqualTo("E"));

            KeyBindings.Assign(interact, Index(entry, 1), "<Keyboard>/f");
            Assert.That(PlayerInteractor.BindingDisplay(interact, KeyBindings.Group), Is.EqualTo("E / F"));

            KeyBindings.Clear(interact, Index(entry, 0));
            Assert.That(PlayerInteractor.BindingDisplay(interact, KeyBindings.Group), Is.EqualTo("F"));
        }

        // ---- 割り当て -------------------------------------------------------

        [Test]
        public void 二つ目の枠にキーを割り当てられる()
        {
            KeyBindingEntry jump = Entry("Jump");
            InputAction action = Action(jump);

            KeyBindings.Assign(action, Index(jump, 1), "<Keyboard>/j");

            Assert.That(action.bindings[Index(jump, 0)].effectivePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(action.bindings[Index(jump, 1)].effectivePath, Is.EqualTo("<Keyboard>/j"));
            Assert.That(KeyBindings.Display(action, Index(jump, 1)), Is.EqualTo("J"));
        }

        [Test]
        public void 別の行と同じキーを割り当ててもよい()
        {
            KeyBindingEntry jump = Entry("Jump");
            KeyBindingEntry interact = Entry("Interact");

            KeyBindings.Assign(Action(jump), Index(jump, 0), "<Keyboard>/e");

            Assert.That(Action(jump).bindings[Index(jump, 0)].effectivePath, Is.EqualTo("<Keyboard>/e"));
            Assert.That(Action(interact).bindings[Index(interact, 0)].effectivePath, Is.EqualTo("<Keyboard>/e"));
        }

        [Test]
        public void 既定と同じキーに戻すと上書きが消える()
        {
            KeyBindingEntry jump = Entry("Jump");
            InputAction action = Action(jump);
            int index = Index(jump, 0);

            KeyBindings.Assign(action, index, "<Keyboard>/j");
            KeyBindings.Assign(action, index, "<Keyboard>/space");

            Assert.That(action.bindings[index].overridePath, Is.Null);
        }

        [Test]
        public void 枠を空にすると表示も空になる()
        {
            KeyBindingEntry sprint = Entry("Sprint");
            InputAction action = Action(sprint);
            int index = Index(sprint, 0);

            KeyBindings.Clear(action, index);

            Assert.That(action.bindings[index].effectivePath, Is.Empty);
            Assert.That(KeyBindings.Display(action, index), Is.EqualTo(KeyBindings.EmptyDisplay));
        }

        [Test]
        public void 行ごとと全体で既定に戻せる()
        {
            KeyBindingEntry jump = Entry("Jump");
            KeyBindingEntry up = Entry("Move", "up");
            KeyBindings.Assign(Action(jump), Index(jump, 1), "<Keyboard>/j");
            KeyBindings.Clear(Action(up), Index(up, 0));

            KeyBindings.ResetEntry(actions, jump);
            Assert.That(Action(jump).bindings[Index(jump, 1)].effectivePath, Is.Empty);
            Assert.That(Action(up).bindings[Index(up, 0)].effectivePath, Is.Empty, "他の行はそのまま");

            KeyBindings.ResetAll(actions);
            Assert.That(Action(up).bindings[Index(up, 0)].effectivePath, Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void 保存したキー設定を読み直せる()
        {
            KeyBindingEntry jump = Entry("Jump");
            KeyBindingEntry left = Entry("Move", "left");
            KeyBindings.Assign(Action(jump), Index(jump, 1), "<Mouse>/rightButton");
            KeyBindings.Assign(Action(left), Index(left, 1), "<Keyboard>/leftArrow");
            KeyBindings.Clear(Action(jump), Index(jump, 0));
            string json = actions.SaveBindingOverridesAsJson();

            InputActionAsset reloaded = LoadActions();
            try
            {
                reloaded.LoadBindingOverridesFromJson(json);
                InputAction reloadedJump = reloaded.FindAction("Jump", true);
                InputAction reloadedMove = reloaded.FindAction("Move", true);

                Assert.That(reloadedJump.bindings[Index(jump, 0)].effectivePath, Is.Empty);
                Assert.That(reloadedJump.bindings[Index(jump, 1)].effectivePath, Is.EqualTo("<Mouse>/rightButton"));
                Assert.That(reloadedMove.bindings[Index(left, 1)].effectivePath, Is.EqualTo("<Keyboard>/leftArrow"));
            }
            finally
            {
                Object.DestroyImmediate(reloaded);
            }
        }

        // ---- 視点 ------------------------------------------------------------

        [Test]
        public void 感度は範囲に収める()
        {
            Assert.That(LookSettings.ClampSensitivity(0f), Is.EqualTo(LookSettings.MinSensitivity));
            Assert.That(LookSettings.ClampSensitivity(99f), Is.EqualTo(LookSettings.MaxSensitivity));
            Assert.That(LookSettings.ClampSensitivity(float.NaN), Is.EqualTo(LookSettings.DefaultSensitivity));
            Assert.That(LookSettings.ClampSensitivity(1.5f), Is.EqualTo(1.5f));
        }

        [Test]
        public void プロセッサ指定から元の倍率と向きを読む()
        {
            LookSettings.ReadDefaults("InvertVector2(invertX=false),ScaleVector2(x=0.05,y=0.05)", out Vector2 scale, out bool invertY);
            Assert.That(scale.x, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(scale.y, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(invertY, Is.True, "InvertVector2 は invertY を省くと反転する");

            LookSettings.ReadDefaults("", out scale, out invertY);
            Assert.That(scale, Is.EqualTo(Vector2.one));
            Assert.That(invertY, Is.False);
        }

        [Test]
        public void 感度と上下反転はマウスのバインドだけに掛かる()
        {
            InputAction look = actions.FindAction("Look", true);
            InputBinding mouse = InputBinding.MaskByGroup("KeyboardMouse");
            InputBinding pad = InputBinding.MaskByGroup("Gamepad");

            Assert.That(LookSettings.Apply(look, 2f, invertY: true), Is.True);
            Assert.That(look.GetParameterValue("ScaleVector2:x", mouse)?.ToSingle(), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(look.GetParameterValue("ScaleVector2:y", mouse)?.ToSingle(), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(look.GetParameterValue("InvertVector2:invertY", mouse)?.ToBoolean(), Is.False, "元が反転なので、上下反転をオンにすると反転しない");
            Assert.That(look.GetParameterValue("ScaleVector2:x", pad)?.ToSingle(), Is.EqualTo(300f).Within(1e-3f), "ゲームパッドはそのまま");

            LookSettings.Apply(look, 1f, invertY: false);
            Assert.That(look.GetParameterValue("ScaleVector2:x", mouse)?.ToSingle(), Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(look.GetParameterValue("InvertVector2:invertY", mouse)?.ToBoolean(), Is.True);
        }
    }
}
