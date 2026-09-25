using System;
using System.Globalization;
using TpsDungeon.Audio.UI;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// ポーズメニューから開く設定画面。音 / 操作 / キー設定の 3 つのタブを持つ。
    /// 値は触ったそばから反映・保存し、ディスクへの書き出しはポーズを解いたときにまとめて行う。
    ///
    /// 動き: タブの下の金の帯が選んだタブへ滑り、ページはタブの移動方向へずれながら入れ替わって、行が一つずつ現れる。
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

        private const string PageFromLeftClass = "rpg-page--from-left";
        private const string PageFromRightClass = "rpg-page--from-right";

        private readonly PlayerControlSettings controls;
        private readonly Button[] tabs = new Button[TabNames.Length];
        private readonly VisualElement[] pages = new VisualElement[PageNames.Length];
        private readonly VisualElement indicator;
        private readonly AudioVolumeSection audio;
        private readonly KeyBindingSection keys;
        private readonly Slider sensitivity;
        private readonly Label sensitivityValue;
        private readonly Toggle invertY;
        private readonly Toggle discreteScroll;
        private readonly Toggle invertScroll;
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
                UiTransitions.HideImmediately(pages[i]);
            }

            // タブの並びが決まってから帯の位置を合わせる（画面の大きさが変わったときも）。
            indicator = root.Q<VisualElement>("tab-indicator");
            root.Q<VisualElement>("tabs")?.RegisterCallback<GeometryChangedEvent>(_ => MoveIndicator());

            // 音
            VisualElement soundPage = pages[(int)Tab.Sound] ?? root;
            audio = new AudioVolumeSection(soundPage, previewClip);
            Clicked(root, "sound-reset", audio.ResetToDefaults);

            // 操作
            sensitivity = root.Q<Slider>("mouse-sensitivity");
            sensitivityValue = root.Q<Label>("mouse-sensitivity-value");
            invertY = root.Q<Toggle>("invert-y");
            discreteScroll = root.Q<Toggle>("discrete-scroll");
            invertScroll = root.Q<Toggle>("invert-scroll");
            if (sensitivity != null)
            {
                sensitivity.lowValue = LookSettings.MinSensitivity;
                sensitivity.highValue = LookSettings.MaxSensitivity;
                SliderDecor.Attach(sensitivity);
                sensitivity.RegisterValueChangedCallback(evt =>
                {
                    controls?.SetMouseSensitivity(evt.newValue);
                    ShowSensitivity(evt.newValue);
                    UiTransitions.Flash(sensitivityValue, "is-flash", 90);
                });
            }

            invertY?.RegisterValueChangedCallback(evt => controls?.SetInvertY(evt.newValue));
            discreteScroll?.RegisterValueChangedCallback(evt => controls?.SetDiscreteScroll(evt.newValue));
            invertScroll?.RegisterValueChangedCallback(evt => controls?.SetInvertScroll(evt.newValue));
            Clicked(root, "controls-reset", () =>
            {
                controls?.ResetControls();
                PullControls();
            });
            pages[(int)Tab.Controls]?.SetEnabled(controls != null);

            // キー設定
            keys = new KeyBindingSection(pages[(int)Tab.Keys] ?? root, controls);

            Clicked(root, "back-button", () => BackRequested?.Invoke());
            current = Tab.Sound;
            MarkSelected();
        }

        /// <summary>キー待ちの最中など、Esc をこの画面が使っているか。</summary>
        public bool IsBusy => keys.IsBusy;

        /// <summary>開くたびに今の設定を読み直し、今のタブのページを出し直す。</summary>
        public void Show()
        {
            audio.Pull();
            PullControls();
            keys.Refresh();

            for (int i = 0; i < pages.Length; i++)
            {
                if (i != (int)current) UiTransitions.HideImmediately(pages[i]);
            }

            ShowPage(current, null);
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
            if (tab == current) return;
            if (tab != Tab.Keys) keys.Cancel();

            // 右のタブへ移るなら、今のページは左へ抜け、新しいページは右から入る。
            bool forward = tab > current;
            HidePage(current, forward ? PageFromLeftClass : PageFromRightClass);
            ShowPage(tab, forward ? PageFromRightClass : PageFromLeftClass);

            current = tab;
            MarkSelected();
        }

        private void ShowPage(Tab tab, string fromClass)
        {
            VisualElement page = pages[(int)tab];
            if (page == null) return;

            SetPageDirection(page, fromClass);
            UiTransitions.Show(page);
            UiTransitions.Stagger(RowsOf(tab, page), 45, 90);
        }

        private void HidePage(Tab tab, string toClass)
        {
            VisualElement page = pages[(int)tab];
            if (page == null) return;

            SetPageDirection(page, toClass);
            UiTransitions.ClearStagger(RowsOf(tab, page));
            UiTransitions.Hide(page);
        }

        private static void SetPageDirection(VisualElement page, string directionClass)
        {
            page.EnableInClassList(PageFromLeftClass, directionClass == PageFromLeftClass);
            page.EnableInClassList(PageFromRightClass, directionClass == PageFromRightClass);
        }

        /// <summary>時間差で現れる行の入れ物。</summary>
        private static VisualElement RowsOf(Tab tab, VisualElement page)
        {
            if (tab == Tab.Keys) return page.Q<ScrollView>("key-list")?.contentContainer;
            return page.Q<VisualElement>(className: "page__rows");
        }

        private void MarkSelected()
        {
            for (int i = 0; i < tabs.Length; i++) tabs[i]?.EnableInClassList("tab--selected", i == (int)current);
            MoveIndicator();
        }

        /// <summary>金の帯を今のタブの下へ動かす。動きは USS の left / width のトランジション。</summary>
        private void MoveIndicator()
        {
            Button tab = tabs[(int)current];
            if (indicator == null || tab == null) return;

            Rect layout = tab.layout;
            if (float.IsNaN(layout.width) || layout.width <= 0f) return;

            indicator.style.left = layout.x;
            indicator.style.width = layout.width;
        }

        private void PullControls()
        {
            if (controls == null) return;

            if (sensitivity != null)
            {
                sensitivity.SetValueWithoutNotify(controls.MouseSensitivity);
                SliderDecor.Refresh(sensitivity);
            }

            ShowSensitivity(controls.MouseSensitivity);
            invertY?.SetValueWithoutNotify(controls.InvertY);
            discreteScroll?.SetValueWithoutNotify(controls.DiscreteScroll);
            invertScroll?.SetValueWithoutNotify(controls.InvertScroll);
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
