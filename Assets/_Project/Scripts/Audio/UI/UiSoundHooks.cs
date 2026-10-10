using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using UnityEngine.UIElements;

namespace TpsDungeon.Audio.UI
{
    /// <summary>
    /// UI Toolkit の画面のボタン・トグルに操作音を付ける。画面の根に 1 回 <see cref="Attach"/> すれば、
    /// その下のボタンとトグルは合わせると Hover、押すと Click が鳴る（ゲームパッドの決定も Click）。
    ///
    /// 押したあとに画面のほうで別の音（戻る・閉じる・タブ）を鳴らすボタンには <see cref="SilentClass"/> を付けて、
    /// Click が重ならないようにする。押せない（無効の）ボタンは鳴らさない。
    /// インベントリの枠のようなボタンでない物は、画面のほうで <see cref="GameAudio.PlayUi"/> を呼ぶ。
    /// </summary>
    public static class UiSoundHooks
    {
        /// <summary>このクラスの付いたボタンは押しても Click を鳴らさない（合わせたときの Hover は鳴らす）。</summary>
        public const string SilentClass = "ui-sound--silent";

        public static void Attach(VisualElement root)
        {
            if (root == null) return;
            root.RegisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
        }

        public static void Detach(VisualElement root)
        {
            if (root == null) return;
            root.UnregisterCallback<PointerEnterEvent>(OnPointerEnter, TrickleDown.TrickleDown);
            root.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            root.UnregisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
        }

        /// <summary>ボタンに付けて、押しても Click を鳴らさないようにする。</summary>
        public static void Silence(VisualElement element) => element?.AddToClassList(SilentClass);

        // 入る要素ごとに飛んでくるので、ボタン・トグルそのものに入ったときだけ鳴らす（中の文字や印に入っても鳴らさない）。
        private static void OnPointerEnter(PointerEnterEvent evt)
        {
            if (evt.target is VisualElement element && IsControl(element) && element.enabledInHierarchy) Play(UiSound.Hover);
        }

        private static void OnClick(ClickEvent evt) => PlayClick(evt.target as VisualElement);

        private static void OnSubmit(NavigationSubmitEvent evt) => PlayClick(evt.target as VisualElement);

        private static void PlayClick(VisualElement target)
        {
            VisualElement control = ControlAt(target);
            if (control == null || !control.enabledInHierarchy || control.ClassListContains(SilentClass)) return;
            Play(UiSound.Click);
        }

        /// <summary>element かその祖先のボタン・トグル。無ければ null。</summary>
        private static VisualElement ControlAt(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.hierarchy.parent)
                if (IsControl(e)) return e;
            return null;
        }

        private static bool IsControl(VisualElement element) => element is Button || element is Toggle;

        private static void Play(UiSound sound) => GameAudio.Instance?.PlayUi(sound);
    }
}
