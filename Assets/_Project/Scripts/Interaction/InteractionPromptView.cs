using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// 画面中央の照準と、その下の「[E] 開ける」のようなプロンプト。
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
        private bool shown;

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
            SetVisible(false, immediate: true);
        }

        private void LateUpdate()
        {
            if (prompt == null) return;

            var target = interactor != null ? interactor.CurrentTarget : null;
            SetVisible(target != null);
            if (target == null) return;

            SetText(keyLabel, KeyCapText(interactor.InteractBindingDisplay));
            SetText(actionLabel, target.PromptLabel);
        }

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
