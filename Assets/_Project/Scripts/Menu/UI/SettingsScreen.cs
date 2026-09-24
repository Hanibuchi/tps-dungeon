using System;
using System.Globalization;
using TpsDungeon.Audio.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// ポーズメニューから開く設定画面。サウンド / 操作 / キー設定の 3 つのタブを持つ。
    /// 値は触ったそばから反映・保存し、ディスクへの書き出しはポーズを解いたときにまとめて行う。
    /// </summary>
    internal sealed class SettingsScreen : IDisposable
    {
        private enum Tab
        {
            Sound,
            Controls,
            Keys,
        }

        private static readonly string[] TabNames = { "tab-sound", "tab-controls", "tab-keys" };
        private static readonly string[] PageNames = { "page-sound", "page-controls", "page-keys" };

        private readonly PlayerControlSettings controls;
        private readonly Button[] tabs = new Button[TabNames.Length];
        private readonly VisualElement[] pages = new VisualElement[PageNames.Length];
        private readonly AudioVolumeSection audio;
        private readonly KeyBindingSection keys;
        private readonly Slider sensitivity;
        private readonly Label sensitivityValue;
        private readonly Toggle invertY;
        private Tab current;

        /// <summary>「戻る」が押された。</summary>
        public event Action BackRequested;

        public SettingsScreen(VisualElement root, PlayerControlSettings controls, AudioClip previewClip)
        {
            this.controls = controls;

            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (Tab)i;
                tabs[i] = root.Q<Button>(TabNames[i]);
                pages[i] = root.Q<VisualElement>(PageNames[i]);
                if (tabs[i] != null) tabs[i].clicked += () => Select(tab);
            }

            // サウンド
            VisualElement soundPage = pages[(int)Tab.Sound] ?? root;
            audio = new AudioVolumeSection(soundPage, previewClip);
            Clicked(root, "sound-reset", audio.ResetToDefaults);

            // 操作
            sensitivity = root.Q<Slider>("mouse-sensitivity");
            sensitivityValue = root.Q<Label>("mouse-sensitivity-value");
            invertY = root.Q<Toggle>("invert-y");
            if (sensitivity != null)
            {
                sensitivity.lowValue = LookSettings.MinSensitivity;
                sensitivity.highValue = LookSettings.MaxSensitivity;
                sensitivity.RegisterValueChangedCallback(evt =>
                {
                    controls?.SetMouseSensitivity(evt.newValue);
                    ShowSensitivity(evt.newValue);
                });
            }

            invertY?.RegisterValueChangedCallback(evt => controls?.SetInvertY(evt.newValue));
            Clicked(root, "controls-reset", () =>
            {
                controls?.ResetLook();
                PullControls();
            });
            pages[(int)Tab.Controls]?.SetEnabled(controls != null);

            // キー設定
            keys = new KeyBindingSection(pages[(int)Tab.Keys] ?? root, controls);

            Clicked(root, "back-button", () => BackRequested?.Invoke());
            Select(Tab.Sound);
        }

        /// <summary>キー待ちの最中など、Esc をこの画面が使っているか。</summary>
        public bool IsBusy => keys.IsBusy;

        /// <summary>開くたびに今の設定を読み直す。</summary>
        public void Show()
        {
            audio.Pull();
            PullControls();
            keys.Refresh();
            Select(current);
        }

        /// <summary>閉じるときにキー待ちを取り消す。</summary>
        public void Hide()
        {
            keys.Cancel();
        }

        public void Dispose()
        {
            audio.Dispose();
            keys.Dispose();
        }

        private void Select(Tab tab)
        {
            if (tab != Tab.Keys) keys?.Cancel();

            current = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                bool selected = i == (int)tab;
                if (pages[i] != null) pages[i].style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                tabs[i]?.EnableInClassList("tab--selected", selected);
            }
        }

        private void PullControls()
        {
            if (controls == null) return;

            sensitivity?.SetValueWithoutNotify(controls.MouseSensitivity);
            ShowSensitivity(controls.MouseSensitivity);
            invertY?.SetValueWithoutNotify(controls.InvertY);
        }

        private void ShowSensitivity(float value)
        {
            if (sensitivityValue != null) sensitivityValue.text = value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void Clicked(VisualElement root, string name, Action action)
        {
            var button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }
    }
}
