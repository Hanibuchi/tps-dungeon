using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace TpsDungeon.Menu
{
    /// <summary>キー設定の 1 行。移動のように 1 つのアクションが上下左右に分かれるものは Part で指す。</summary>
    public readonly struct KeyBindingEntry
    {
        public readonly string Label;
        public readonly string ActionName;

        /// <summary>複合バインドの部品名（"up" など）。単独のバインドなら null。</summary>
        public readonly string Part;

        public KeyBindingEntry(string label, string actionName, string part = null)
        {
            Label = label;
            ActionName = actionName;
            Part = part;
        }
    }

    /// <summary>
    /// キーボード・マウスのキー設定。1 つの行に 2 つまでキーを割り当てられる。
    /// 枠は入力アセットに 2 つずつ用意してあり（2 つ目は空）、ここではその枠の上書きだけを扱う。
    /// 同じキーを複数の行に割り当てるのは許す。
    /// </summary>
    public static class KeyBindings
    {
        /// <summary>1 行あたりの枠の数。</summary>
        public const int SlotCount = 2;

        /// <summary>キー設定で扱うコントロールスキーム。</summary>
        public const string Group = "KeyboardMouse";

        /// <summary>設定画面に並べる行。視点（マウス）とホイール、ポーズ（Esc）は固定なので含めない。</summary>
        public static readonly IReadOnlyList<KeyBindingEntry> Entries = new[]
        {
            new KeyBindingEntry("前進", "Move", "up"),
            new KeyBindingEntry("後退", "Move", "down"),
            new KeyBindingEntry("左", "Move", "left"),
            new KeyBindingEntry("右", "Move", "right"),
            new KeyBindingEntry("ジャンプ", "Jump"),
            new KeyBindingEntry("ダッシュ", "Sprint"),
            new KeyBindingEntry("インタラクト", "Interact"),
            new KeyBindingEntry("マップ", "Map"),
            new KeyBindingEntry("ホットバー 1", "HotbarSlot1"),
            new KeyBindingEntry("ホットバー 2", "HotbarSlot2"),
            new KeyBindingEntry("ホットバー 3", "HotbarSlot3"),
            new KeyBindingEntry("ホットバー 4", "HotbarSlot4"),
        };

        /// <summary>空の枠の表示。</summary>
        public const string EmptyDisplay = "—";

        /// <summary>
        /// 行の slot 番目（0 始まり）の枠に当たるバインドの番号。見つからなければ -1。
        /// キーボード・マウスのスキームに属するバインドを、入力アセットに並んでいる順に数える。
        /// </summary>
        public static int FindBindingIndex(InputAction action, string part, int slot)
        {
            if (action == null || slot < 0) return -1;

            int seen = 0;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || !InGroup(binding)) continue;

                bool wanted = part == null
                    ? !binding.isPartOfComposite
                    : binding.isPartOfComposite && string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase);
                if (!wanted) continue;

                if (seen == slot) return i;
                seen++;
            }

            return -1;
        }

        /// <summary>枠にキーを割り当てる。path は "&lt;Keyboard&gt;/space" のような形。</summary>
        public static void Assign(InputAction action, int bindingIndex, string path)
        {
            if (!IsValid(action, bindingIndex)) return;

            // 入力アセットの既定と同じなら上書きを消す。保存が既定と食い違わないように。
            if (string.Equals(action.bindings[bindingIndex].path ?? string.Empty, path ?? string.Empty, StringComparison.Ordinal))
                action.RemoveBindingOverride(bindingIndex);
            else
                action.ApplyBindingOverride(bindingIndex, path ?? string.Empty);
        }

        /// <summary>枠を空にする。空文字で上書きすると、そのバインドはどのキーにも反応しなくなる。</summary>
        public static void Clear(InputAction action, int bindingIndex)
        {
            Assign(action, bindingIndex, string.Empty);
        }

        /// <summary>行の枠を全部入力アセットの既定に戻す。</summary>
        public static void ResetEntry(InputActionAsset actions, KeyBindingEntry entry)
        {
            InputAction action = actions != null ? actions.FindAction(entry.ActionName) : null;
            if (action == null) return;

            for (int slot = 0; slot < SlotCount; slot++)
            {
                int index = FindBindingIndex(action, entry.Part, slot);
                if (index >= 0) action.RemoveBindingOverride(index);
            }
        }

        /// <summary>キー設定の全行を既定に戻す。キー設定で扱わないバインドの上書きには触らない。</summary>
        public static void ResetAll(InputActionAsset actions)
        {
            foreach (KeyBindingEntry entry in Entries) ResetEntry(actions, entry);
        }

        /// <summary>枠に今割り当たっているキーの表示名。空なら <see cref="EmptyDisplay"/>。</summary>
        public static string Display(InputAction action, int bindingIndex)
        {
            if (!IsValid(action, bindingIndex)) return EmptyDisplay;
            if (string.IsNullOrEmpty(action.bindings[bindingIndex].effectivePath)) return EmptyDisplay;

            string text = action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrEmpty(text) ? EmptyDisplay : text;
        }

        private static bool IsValid(InputAction action, int bindingIndex)
        {
            return action != null && bindingIndex >= 0 && bindingIndex < action.bindings.Count;
        }

        private static bool InGroup(InputBinding binding)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            foreach (string group in binding.groups.Split(InputBinding.Separator))
            {
                if (string.Equals(group, Group, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
