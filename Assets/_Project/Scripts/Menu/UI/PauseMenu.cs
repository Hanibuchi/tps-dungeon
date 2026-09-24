using System;
using TpsDungeon.Audio.Data;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// Esc（ゲームパッドは Start）で開くポーズメニュー。メニューから設定画面へ進める。
    /// PauseMenu.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    ///
    /// ポーズ中は次のようにしてゲームを止める。
    /// - timeScale を 0 にする
    /// - PlayerInput を UI マップに切り替えて、移動・視点・ホットバーなどの入力を止める
    ///   （マウスの視点移動は deltaTime を掛けないので、timeScale だけでは止まらない）
    /// - カーソルの固定を外す
    /// - 音を Paused スナップショットでこもらせる
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Pause Menu")]
    public sealed class PauseMenu : MonoBehaviour
    {
        private enum Page
        {
            Closed,
            Menu,
            Settings,
        }

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定なら親から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("操作の設定の窓口。未設定なら親から探す。")]
        private PlayerControlSettings controls;

        [SerializeField, Tooltip("ポーズするときに閉じる大きな地図。未設定なら親から探す。")]
        private PlayerMapToggle mapToggle;

        [SerializeField, Tooltip("SE スライダーを動かしたときに鳴らす確認用の音。")]
        private AudioClip previewClip;

        [SerializeField, Tooltip("ポーズを開くアクション（ゲーム中のマップ側）。")]
        private string pauseActionName = "Player/Pause";

        [SerializeField, Tooltip("一つ戻る / ポーズを解くアクション（UI マップ側）。")]
        private string cancelActionName = "UI/Cancel";

        [SerializeField, Tooltip("ゲーム中に使うアクションマップ。")]
        private string gameplayMapName = "Player";

        [SerializeField, Tooltip("ポーズ中に使うアクションマップ。")]
        private string menuMapName = "UI";

        private UIDocument document;
        private VisualElement scrim;
        private VisualElement menuPanel;
        private VisualElement settingsPanel;
        private Button resumeButton;
        private SettingsScreen settings;
        private InputAction pauseAction;
        private InputAction cancelAction;

        private Page page = Page.Closed;
        private int pausedFrame = -1;
        private float timeScaleBeforePause = 1f;
        private AudioSnapshotId snapshotBeforePause = AudioSnapshotId.Dungeon;

        /// <summary>ポーズ中か（メニューでも設定画面でも）。</summary>
        public bool IsPaused => page != Page.Closed;

        /// <summary>ポーズの開け閉めや、メニューと設定画面の行き来で飛ぶ。</summary>
        public event Action<PauseMenu> Changed;

        private void Reset()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            controls = GetComponentInParent<PlayerControlSettings>();
            mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (controls == null) controls = GetComponentInParent<PlayerControlSettings>();
            if (mapToggle == null) mapToggle = GetComponentInParent<PlayerMapToggle>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            scrim = root.Q<VisualElement>("scrim");
            menuPanel = root.Q<VisualElement>("menu-panel");
            settingsPanel = root.Q<VisualElement>("settings-panel");
            resumeButton = root.Q<Button>("resume-button");

            if (resumeButton != null) resumeButton.clicked += Resume;
            var settingsButton = root.Q<Button>("settings-button");
            if (settingsButton != null) settingsButton.clicked += ShowSettings;
            var quitButton = root.Q<Button>("quit-button");
            if (quitButton != null) quitButton.clicked += Quit;

            settings = new SettingsScreen(settingsPanel ?? root, controls, previewClip);
            settings.BackRequested += ShowMenu;

            ShowPage(Page.Closed);
        }

        private void OnDisable()
        {
            // シーンを抜けるなどでポーズ中に消えても、時間やカーソルを止めたままにしない。
            if (IsPaused) Resume();

            settings?.Dispose();
            settings = null;
        }

        // PlayerInput は OnEnable でアクションを用意し直すことがあるので、掴むのは全員の OnEnable の後。
        private void Start()
        {
            InputActionAsset actions = playerInput != null ? playerInput.actions : null;
            if (actions == null)
            {
                Debug.LogWarning("PlayerInput が無いのでポーズできない", this);
                return;
            }

            pauseAction = actions.FindAction(pauseActionName);
            cancelAction = actions.FindAction(cancelActionName);
            if (pauseAction == null) Debug.LogWarning($"アクション '{pauseActionName}' が {actions.name} に無い", this);
            if (cancelAction == null) Debug.LogWarning($"アクション '{cancelActionName}' が {actions.name} に無い", this);
        }

        private void Update()
        {
            if (!IsPaused)
            {
                if (pauseAction != null && pauseAction.WasPressedThisFrame()) Pause();
                return;
            }

            // 開いたのと同じ押下で閉じないように。
            if (Time.frameCount == pausedFrame) return;
            if (cancelAction == null || !cancelAction.WasPressedThisFrame()) return;

            // キー待ちを Esc で取り消したときは、その Esc で画面を戻さない。
            if (settings != null && settings.IsBusy) return;

            if (page == Page.Settings) ShowMenu();
            else Resume();
        }

        private void LateUpdate()
        {
            // Starter Assets はウィンドウにフォーカスが戻るとカーソルを固定し直すので、ポーズ中は外し続ける。
            if (IsPaused && Cursor.lockState != CursorLockMode.None) UnlockCursor();
        }

        /// <summary>ポーズしてメニューを出す。</summary>
        public void Pause()
        {
            if (IsPaused) return;

            pausedFrame = Time.frameCount;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;

            if (mapToggle != null) mapToggle.SetOpen(false);

            if (playerInput != null)
            {
                playerInput.SwitchCurrentActionMap(menuMapName);
                ReleaseCharacterInput();
            }

            UnlockCursor();

            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                snapshotBeforePause = audio.CurrentSnapshot;
                audio.TransitionTo(AudioSnapshotId.Paused);
            }

            ShowPage(Page.Menu);
        }

        /// <summary>ポーズを解いてゲームに戻る。</summary>
        public void Resume()
        {
            if (!IsPaused) return;

            ShowPage(Page.Closed);

            controls?.Flush();
            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                audio.Volumes?.Flush();
                audio.TransitionTo(snapshotBeforePause);
            }

            if (playerInput != null) playerInput.SwitchCurrentActionMap(gameplayMapName);
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void ShowMenu()
        {
            if (IsPaused) ShowPage(Page.Menu);
        }

        private void ShowSettings()
        {
            if (IsPaused) ShowPage(Page.Settings);
        }

        private void ShowPage(Page next)
        {
            if (page == Page.Settings && next != Page.Settings) settings?.Hide();

            page = next;
            SetVisible(scrim, next != Page.Closed);
            SetVisible(menuPanel, next == Page.Menu);
            SetVisible(settingsPanel, next == Page.Settings);

            if (next == Page.Settings) settings?.Show();
            if (next == Page.Menu) resumeButton?.Focus();

            Changed?.Invoke(this);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// キャラが握っている入力を離させる。マップを切り替えても、止める直前の移動や視点の値が残り、
        /// ポーズを解いた瞬間に歩き出すことがあるため。
        /// StarterAssetsInputs の公開メソッドを名前で呼ぶので、Starter Assets のアセンブリには依存しない。
        /// </summary>
        private void ReleaseCharacterInput()
        {
            GameObject target = playerInput.gameObject;
            target.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("LookInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("JumpInput", false, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Quit()
        {
            controls?.Flush();
            GameAudio.Instance?.Volumes?.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
