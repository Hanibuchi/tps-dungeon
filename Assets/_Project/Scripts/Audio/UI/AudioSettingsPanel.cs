using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Audio.UI
{
    /// <summary>
    /// 全体 / BGM / SE の音量を触る設定画面。
    /// AudioSettingsPanel.uxml を UIDocument に差して使う。
    /// 今はタイトルやポーズ画面が無いので、単体で開閉できる形にしてある。
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

        [SerializeField, Tooltip("Esc キーで開閉できるようにする。")]
        private bool toggleWithEscape = true;

        [SerializeField, Tooltip("SE スライダーを動かしたときに鳴らす確認用の音。")]
        private AudioClip previewClip;

        private UIDocument document;
        private VisualElement scrim;
        private readonly Slider[] sliders = new Slider[3];
        private readonly Label[] values = new Label[3];
        private float nextPreviewTime;

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

            Bind(AudioChannel.Master, "master-volume", "master-value");
            Bind(AudioChannel.Bgm, "bgm-volume", "bgm-value");
            Bind(AudioChannel.Se, "se-volume", "se-value");

            root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());
            root.Q<Button>("reset-button")?.RegisterCallback<ClickEvent>(_ =>
            {
                GameAudio.Instance?.Volumes?.ResetToDefaults();
                Pull();
            });

            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.Volumes.VolumeChanged += OnVolumeChanged;
            }

            Pull();

            // 起動時の初期化ではスナップショットを触らない。
            // 閉じた状態を作るだけのつもりで、ミキサーの状態まで書き換えてしまわないように。
            SetOpen(openOnStart, openOnStart);
        }

        private void OnDisable()
        {
            if (GameAudio.Instance != null && GameAudio.Instance.Volumes != null)
            {
                GameAudio.Instance.Volumes.VolumeChanged -= OnVolumeChanged;
            }
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
            IsOpen = open;
            if (scrim != null)
            {
                scrim.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (open) Pull();

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

        private void Bind(AudioChannel channel, string sliderName, string valueName)
        {
            VisualElement root = document.rootVisualElement;
            var slider = root.Q<Slider>(sliderName);
            var label = root.Q<Label>(valueName);

            sliders[(int)channel] = slider;
            values[(int)channel] = label;
            if (slider == null) return;

            slider.RegisterValueChangedCallback(evt =>
            {
                GameAudio.Instance?.Volumes?.SetVolume(channel, evt.newValue);
                Show(channel, evt.newValue);
                if (channel == AudioChannel.Se) PlayPreview();
            });
        }

        /// <summary>今の音量をスライダーに反映する。</summary>
        private void Pull()
        {
            AudioVolumeController volumes = GameAudio.Instance?.Volumes;
            if (volumes == null) return;

            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                float value = volumes.GetVolume(channel);
                Slider slider = sliders[(int)channel];
                if (slider != null) slider.SetValueWithoutNotify(value);
                Show(channel, value);
            }
        }

        private void OnVolumeChanged(AudioChannel channel, float value)
        {
            Slider slider = sliders[(int)channel];
            if (slider != null) slider.SetValueWithoutNotify(value);
            Show(channel, value);
        }

        private void Show(AudioChannel channel, float value)
        {
            Label label = values[(int)channel];
            if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        /// <summary>SE を触っている間、音量が分かるように短い音を鳴らす。鳴らしすぎないよう間引く。</summary>
        private void PlayPreview()
        {
            if (previewClip == null || Time.unscaledTime < nextPreviewTime) return;

            nextPreviewTime = Time.unscaledTime + 0.12f;
            GameAudio.Instance?.PlaySe(previewClip);
        }
    }
}
