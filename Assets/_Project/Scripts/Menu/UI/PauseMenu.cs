using System;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Player;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// Esc（ゲームパッドは Start）で開くポーズメニュー。メニューから設定画面へ進める。
    /// PauseMenu.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    ///
    /// ポーズ中の止め方（timeScale・入力・カーソル・音）は <see cref="GamePauser"/> にある。
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
        private VisualElement menuItems;
        private Button resumeButton;
        private SettingsScreen settings;
        private InputAction pauseAction;
        private InputAction cancelAction;
        private GamePauser pauser;

        // 再生中にスクリプトを再コンパイルすると、シリアライズされないこのフィールドだけ消えて Awake も呼ばれ直さない。
        // そのときも止まらないよう、使うときに作る。
        private GamePauser Pauser => pauser ??= new GamePauser(playerInput, controls, mapToggle, gameplayMapName, menuMapName);

        private Page page = Page.Closed;
        private int pausedFrame = -1;

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
            menuItems = root.Q<VisualElement>("menu-items");
            resumeButton = root.Q<Button>("resume-button");

            if (resumeButton != null) resumeButton.clicked += Resume;
            var settingsButton = root.Q<Button>("settings-button");
            if (settingsButton != null) settingsButton.clicked += ShowSettings;
            var quitButton = root.Q<Button>("quit-button");
            if (quitButton != null) quitButton.clicked += Quit;

            settings = new SettingsScreen(settingsPanel ?? root, controls, previewClip);
            settings.BackRequested += ShowMenu;

            ShowPage(Page.Closed, animate: false);
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
            Pauser.KeepCursorFree();
        }

        /// <summary>ポーズしてメニューを出す。</summary>
        public void Pause()
        {
            if (IsPaused) return;

            pausedFrame = Time.frameCount;
            Pauser.Pause();
            ShowPage(Page.Menu);
        }

        /// <summary>ポーズを解いてゲームに戻る。</summary>
        public void Resume()
        {
            if (!IsPaused) return;

            ShowPage(Page.Closed);
            Pauser.Resume();
        }

        private void ShowMenu()
        {
            if (IsPaused) ShowPage(Page.Menu);
        }

        private void ShowSettings()
        {
            if (IsPaused) ShowPage(Page.Settings);
        }

        /// <summary>
        /// ページを切り替える。見た目の動きは USS（Theme.uss / PauseMenu.uss）のトランジションで、ここでは向きだけ決める。
        /// - 開く・閉じる: パネルが下からせり上がる / 沈む
        /// - メニュー → 設定: メニューが左へ抜け、設定が右から入る（戻るときは逆）
        /// </summary>
        private void ShowPage(Page next, bool animate = true)
        {
            Page previous = page;
            if (previous == Page.Settings && next != Page.Settings) settings?.Hide();
            page = next;

            if (!animate)
            {
                UiTransitions.HideImmediately(scrim);
                UiTransitions.HideImmediately(menuPanel);
                UiTransitions.HideImmediately(settingsPanel);
                Changed?.Invoke(this);
                return;
            }

            switch (next)
            {
                case Page.Closed:
                    // 閉じるときは横へ抜けず、その場で沈む。
                    SetDirection(menuPanel, null);
                    SetDirection(settingsPanel, null);
                    UiTransitions.ClearStagger(menuItems);
                    UiTransitions.Hide(menuPanel);
                    UiTransitions.Hide(settingsPanel);
                    UiTransitions.Hide(scrim);
                    break;

                case Page.Menu:
                    UiTransitions.Show(scrim);
                    SetDirection(menuPanel, previous == Page.Settings ? FromLeftClass : null);
                    SetDirection(settingsPanel, FromRightClass);
                    UiTransitions.Hide(settingsPanel);
                    UiTransitions.Show(menuPanel);
                    // 項目を一つずつ滑り込ませる。最初に開くときはパネルが上がりきるのを少し待つ。
                    UiTransitions.Stagger(menuItems, 70, previous == Page.Closed ? 160 : 60);
                    resumeButton?.Focus();
                    break;

                case Page.Settings:
                    UiTransitions.ClearStagger(menuItems);
                    SetDirection(menuPanel, FromLeftClass);
                    SetDirection(settingsPanel, FromRightClass);
                    UiTransitions.Hide(menuPanel);
                    UiTransitions.Show(settingsPanel);
                    settings?.Show();
                    break;
            }

            Changed?.Invoke(this);
        }

        private const string FromLeftClass = "rpg-panel--from-left";
        private const string FromRightClass = "rpg-panel--from-right";

        private static void SetDirection(VisualElement panel, string directionClass)
        {
            if (panel == null) return;
            panel.EnableInClassList(FromLeftClass, directionClass == FromLeftClass);
            panel.EnableInClassList(FromRightClass, directionClass == FromRightClass);
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
