using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TpsDungeon.UiKit;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 設定画面の「キー設定」タブ。<see cref="KeyBindings.Entries"/> の行を並べ、
    /// 枠をクリックすると次に押したキー（またはマウスボタン）をその枠に割り当てる。
    /// 右クリックで枠を空に、行ごとの「既定」で入力アセットの既定に戻す。
    /// </summary>
    internal sealed class KeyBindingSection : IDisposable
    {
        private const string ListeningText = "キーを押す…";

        // 見た目は PauseMenu.uss。キー待ちの明滅・決まった瞬間の光・空にしたときの沈み。
        private const string PulseClass = "is-pulse";
        private const string FlashClass = "is-flash";
        private const string SinkClass = "is-sink";

        private readonly PlayerControlSettings controls;
        private readonly List<Button[]> slotButtons = new List<Button[]>();
        private InputActionRebindingExtensions.RebindingOperation operation;
        private int lastFinishedFrame = -1;

        public KeyBindingSection(VisualElement page, PlayerControlSettings controls)
        {
            this.controls = controls;

            var list = page.Q<ScrollView>("key-list");
            if (list != null) Build(list);

            var resetAll = page.Q<Button>("keys-reset");
            if (resetAll != null) resetAll.clicked += ResetAll;

            page.SetEnabled(controls != null);
            Refresh();
        }

        /// <summary>
        /// キー待ちの最中か、キー待ちがこのフレームで終わったところか。
        /// キー待ちを Esc で取り消したとき、同じ Esc で設定画面まで閉じてしまわないように見る。
        /// </summary>
        public bool IsBusy => operation != null || lastFinishedFrame == Time.frameCount;

        public void Dispose()
        {
            Cancel();
        }

        /// <summary>キー待ちを取り消す。タブを切り替えたり画面を閉じたりするときに呼ぶ。</summary>
        public void Cancel()
        {
            operation?.Cancel();
        }

        /// <summary>全部の枠の表示を今の割り当てに合わせる。</summary>
        public void Refresh()
        {
            for (int row = 0; row < slotButtons.Count; row++)
            {
                KeyBindingEntry entry = KeyBindings.Entries[row];
                InputAction action = FindAction(entry);
                for (int slot = 0; slot < KeyBindings.SlotCount; slot++)
                {
                    Button button = slotButtons[row][slot];
                    int index = KeyBindings.FindBindingIndex(action, entry.Part, slot);
                    string text = KeyBindings.Display(action, index);
                    button.text = text;
                    button.EnableInClassList("key-slot--empty", text == KeyBindings.EmptyDisplay);
                    button.RemoveFromClassList("key-slot--listening");
                    button.SetEnabled(index >= 0);
                }
            }
        }

        private void Build(ScrollView list)
        {
            list.Clear();
            slotButtons.Clear();

            for (int row = 0; row < KeyBindings.Entries.Count; row++)
            {
                KeyBindingEntry entry = KeyBindings.Entries[row];
                var line = new VisualElement();
                line.AddToClassList("key-row");
                // 行はページが開くときに一つずつ現れる（Theme.uss の .rpg-row）。
                line.AddToClassList("rpg-row");

                var label = new Label(entry.Label);
                label.AddToClassList("key-row__label");
                line.Add(label);

                var buttons = new Button[KeyBindings.SlotCount];
                for (int slot = 0; slot < buttons.Length; slot++)
                {
                    int capturedRow = row;
                    int capturedSlot = slot;
                    var button = new Button(() => StartRebind(capturedRow, capturedSlot)) { name = $"key-{row}-{slot}" };
                    button.AddToClassList("key-slot");

                    // 右クリックで空にする。Button の左クリック処理より先に拾えるよう TrickleDown で受ける。
                    button.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 1) return;
                        ClearSlot(capturedRow, capturedSlot);
                        evt.StopPropagation();
                    }, TrickleDown.TrickleDown);

                    buttons[slot] = button;
                    line.Add(button);
                }

                int resetRow = row;
                var reset = new Button(() => ResetRow(resetRow)) { text = "既定" };
                reset.AddToClassList("rpg-button");
                reset.AddToClassList("rpg-button--small");
                reset.AddToClassList("key-row__reset");
                line.Add(reset);

                slotButtons.Add(buttons);
                list.Add(line);
            }
        }

        private void StartRebind(int row, int slot)
        {
            if (IsBusy) return;

            KeyBindingEntry entry = KeyBindings.Entries[row];
            InputAction action = FindAction(entry);
            int index = KeyBindings.FindBindingIndex(action, entry.Part, slot);
            if (index < 0) return;

            // 割り当て中のアクションは止めておく必要がある。ポーズ中は Player マップごと止まっているはずだが念のため。
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();

            Button button = slotButtons[row][slot];
            button.text = ListeningText;
            button.RemoveFromClassList("key-slot--empty");
            button.AddToClassList("key-slot--listening");
            UiTransitions.Pulse(button, PulseClass, 450);

            operation = action.PerformInteractiveRebinding(index)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Pointer>/press")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(0.1f)
                // 既定と同じキーなら上書きを消す、という扱いを KeyBindings 側に揃える。
                .OnApplyBinding((_, path) => KeyBindings.Assign(action, index, path))
                .OnComplete(_ => Finish(action, wasEnabled, true, button))
                .OnCancel(_ => Finish(action, wasEnabled, false, button))
                .Start();
        }

        private void Finish(InputAction action, bool wasEnabled, bool changed, Button button)
        {
            operation?.Dispose();
            operation = null;
            lastFinishedFrame = Time.frameCount;

            if (wasEnabled) action.Enable();
            if (changed) controls.SaveBindings();

            UiTransitions.StopPulse(button, PulseClass);
            Refresh();

            // 決まった枠を一瞬金に光らせる。
            if (changed) UiTransitions.Flash(button, FlashClass, 140);
        }

        private void ClearSlot(int row, int slot)
        {
            if (IsBusy || controls == null) return;

            KeyBindingEntry entry = KeyBindings.Entries[row];
            InputAction action = FindAction(entry);
            KeyBindings.Clear(action, KeyBindings.FindBindingIndex(action, entry.Part, slot));
            controls.SaveBindings();
            Refresh();
            UiTransitions.Flash(slotButtons[row][slot], SinkClass, 110);
        }

        private void ResetRow(int row)
        {
            if (IsBusy || controls == null) return;

            KeyBindings.ResetEntry(controls.Actions, KeyBindings.Entries[row]);
            controls.SaveBindings();
            Refresh();
            foreach (Button button in slotButtons[row]) UiTransitions.Flash(button, FlashClass, 140);
        }

        private void ResetAll()
        {
            if (IsBusy || controls == null) return;

            KeyBindings.ResetAll(controls.Actions);
            controls.SaveBindings();
            Refresh();
        }

        // PlayerInput がアクションを作り直すことがあるので、行を作ったときに掴まず毎回引く。
        private InputAction FindAction(KeyBindingEntry entry)
        {
            InputActionAsset actions = controls != null ? controls.Actions : null;
            return actions != null ? actions.FindAction(entry.ActionName) : null;
        }
    }
}
