using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// 画面中央の照準と、その下の「[E] 開ける」のようなプロンプト。
    /// 狙っている相手が IInteractableDetails を実装していれば、画面右側に情報欄（絵・名前・説明）も出す。
    /// InteractionHud.uxml を UIDocument に差して、プレイヤーの子に置いて使う。
    /// 表示は PlayerInteractor の状態を毎フレーム写すだけで、入力は扱わない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Interaction Prompt View")]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField, Tooltip("状態を写す相手。未設定なら親から探す。")]
        private PlayerInteractor interactor;

        private UIDocument document;
        private VisualElement prompt;
        private Label keyLabel;
        private Label actionLabel;
        private VisualElement reticle;
        private VisualElement details;
        private VisualElement detailsIcon;
        private Label detailsTitle;
        private Label detailsBody;
        private bool shown;
        private bool detailsShown;
        private IInteractableDetails detailsSource;

        private void Reset()
        {
            interactor = GetComponentInParent<PlayerInteractor>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            // HUD はクリックを受けない。下の設定画面などの操作を邪魔しないように。
            root.pickingMode = PickingMode.Ignore;
            prompt = root.Q<VisualElement>("prompt");
            keyLabel = root.Q<Label>("prompt-key");
            actionLabel = root.Q<Label>("prompt-action");
            reticle = root.Q<VisualElement>("reticle");
            details = root.Q<VisualElement>("details");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsTitle = root.Q<Label>("details-title");
            detailsBody = root.Q<Label>("details-body");
            SetVisible(false, immediate: true);
            SetDetails(null, immediate: true);
        }

        private void LateUpdate()
        {
            if (prompt == null) return;

            var target = interactor != null ? interactor.CurrentTarget : null;
            SetVisible(target != null);
            SetDetails(DetailsOf(target));
            if (target == null) return;

            SetText(keyLabel, KeyCapText(interactor.InteractBindingDisplay));
            SetText(actionLabel, target.PromptLabel);
        }

        /// <summary>
        /// 情報欄を出し入れする。右からすべり込み、右へ抜ける（InteractionHud.uss）。
        /// 消えていく間は最後の中身を残したままにする。
        /// </summary>
        private void SetDetails(IInteractableDetails source, bool immediate = false)
        {
            if (details == null) return;

            bool visible = source != null;
            if (visible && !ReferenceEquals(source, detailsSource))
            {
                detailsSource = source;
                SetText(detailsTitle, source.DetailTitle);
                SetText(detailsBody, source.DetailBody);
                if (detailsIcon != null)
                {
                    Texture2D icon = source.DetailIcon;
                    detailsIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
                    detailsIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            if (!immediate && visible == detailsShown) return;

            detailsShown = visible;
            if (!visible) detailsSource = null;
            if (immediate && !visible) UiTransitions.HideImmediately(details);
            else UiTransitions.SetShown(details, visible);
        }

        /// <summary>情報欄に出すものがあればそれを、無ければ null。</summary>
        public static IInteractableDetails DetailsOf(IInteractable target) => target as IInteractableDetails;

        /// <summary>
        /// 案内を出し入れする。下からせり上がって現れ、沈んで消える（InteractionHud.uss）。
        /// 消えていく間は最後の文字を残したままにする。照準も狙っている間は回って灯る。
        /// </summary>
        private void SetVisible(bool visible, bool immediate = false)
        {
            if (prompt == null) return;
            if (!immediate && visible == shown) return;

            shown = visible;
            reticle?.EnableInClassList("reticle--aimed", visible);
            if (immediate && !visible) UiTransitions.HideImmediately(prompt);
            else UiTransitions.SetShown(prompt, visible);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        /// <summary>キーキャップに出す文字。割り当てが無いときも枠だけ浮かないよう "?" を出す。</summary>
        public static string KeyCapText(string bindingDisplay)
        {
            return string.IsNullOrWhiteSpace(bindingDisplay) ? "?" : bindingDisplay.Trim();
        }
    }
}
