using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Audio.UI
{
    /// <summary>
    /// 全体 / BGM / SE の音量だけを触る単体の設定パネル。
    /// AudioSettingsPanel.uxml を UIDocument に差して使う。
    /// ゲーム中の設定はポーズ画面から開くので、これは音の確認用シーンなどで単体で開閉したいとき向け。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Audio Settings Panel")]
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("開いている間 Paused スナップショットに切り替えて音をこもらせる。")]
        private bool muffleWhileOpen = true;

        [SerializeField, Tooltip("閉じたときに戻すスナップショット。")]
        private AudioSnapshotId closedSnapshot = AudioSnapshotId.Dungeon;

        [SerializeField, Tooltip("起動直後から開いておく。")]
        private bool openOnStart;

        [SerializeField, Tooltip("Esc キーで開閉できるようにする。ポーズ画面と取り合うので、ポーズ画面のあるシーンでは切っておく。")]
        private bool toggleWithEscape;

        [SerializeField, Tooltip("SE スライダーを動かしたときに鳴らす確認用の音。")]
        private AudioClip previewClip;

        private UIDocument document;
        private VisualElement scrim;
        private VisualElement panel;
        private VisualElement page;
        private AudioVolumeSection volumes;

        /// <summary>開いているかどうか。</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            scrim = root.Q<VisualElement>("scrim") ?? root;
            panel = root.Q<VisualElement>("panel");
            page = root.Q<VisualElement>("page");
            volumes = new AudioVolumeSection(root, previewClip);

            root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());
            root.Q<Button>("reset-button")?.RegisterCallback<ClickEvent>(_ => volumes?.ResetToDefaults());

            // 起動時の初期化ではスナップショットを触らない。
            // 閉じた状態を作るだけのつもりで、ミキサーの状態まで書き換えてしまわないように。
            SetOpen(openOnStart, openOnStart);
        }

        private void OnDisable()
        {
            volumes?.Dispose();
            volumes = null;
        }

        private void Update()
        {
            if (!toggleWithEscape || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        private void SetOpen(bool open, bool applySnapshot = true)
        {
            bool wasOpen = IsOpen;
            IsOpen = open;

            // 幕・パネル・行の順に、USS のトランジションで開け閉めする。初期化で閉じるときだけは一瞬で。
            VisualElement[] parts = { scrim, panel, page };
            foreach (VisualElement part in parts)
            {
                if (open) UiTransitions.Show(part);
                else if (wasOpen) UiTransitions.Hide(part);
                else UiTransitions.HideImmediately(part);
            }

            if (open)
            {
                volumes?.Pull();
                UiTransitions.Stagger(page?.Q<TemplateContainer>(), 50, 150);
            }

            GameAudio audio = GameAudio.Instance;
            if (audio == null || !applySnapshot) return;

            if (!open)
            {
                // 閉じるときに設定をディスクへ書き出す。
                audio.Volumes?.Flush();
                audio.TransitionTo(closedSnapshot);
                return;
            }

            if (muffleWhileOpen) audio.TransitionTo(AudioSnapshotId.Paused);
        }
    }
}
