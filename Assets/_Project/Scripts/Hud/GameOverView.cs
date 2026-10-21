using TpsDungeon.Map.Runtime;
using TpsDungeon.Party;
using TpsDungeon.UiKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 全員が倒れたときの「力尽きた」と、倒れた階（「10階 第2層」）。全滅の演出（<see cref="WipeSequence"/>）の知らせで出し入れする。
    /// 飛ばせるようになったら「キーを押して進む」を明滅させる。地上で倒れたときは階の行を出さない。
    /// パーティーの操作台の「Game Over」（UIDocument）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Game Over View")]
    public sealed class GameOverView : MonoBehaviour
    {
        private const string HintDimClass = "game-over__hint--dim";
        private const long HintPulseMs = 900;

        private VisualElement panel;
        private Label floor;
        private Label hint;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            panel = root.Q<VisualElement>("game-over");
            floor = root.Q<Label>("floor");
            hint = root.Q<Label>("hint");
            UiTransitions.HideImmediately(panel);
            UiTransitions.HideImmediately(hint);

            WipeSequence.GameOverShown += OnGameOverShown;
            WipeSequence.SkipReady += OnSkipReady;
            WipeSequence.Ended += OnEnded;
        }

        private void OnDisable()
        {
            WipeSequence.GameOverShown -= OnGameOverShown;
            WipeSequence.SkipReady -= OnSkipReady;
            WipeSequence.Ended -= OnEnded;
        }

        private void OnGameOverShown()
        {
            string text = FloorText();
            if (floor != null)
            {
                floor.text = text ?? string.Empty;
                floor.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UiTransitions.Show(panel);
        }

        private void OnSkipReady()
        {
            UiTransitions.Show(hint);
            UiTransitions.Pulse(hint, HintDimClass, HintPulseMs);
        }

        private void OnEnded()
        {
            UiTransitions.StopPulse(hint, HintDimClass);
            UiTransitions.HideImmediately(hint);
            UiTransitions.HideImmediately(panel);
        }

        /// <summary>倒れた階の札。階でなければ null。</summary>
        private static string FloorText()
        {
            var bootstrap = FindAnyObjectByType<FloorBootstrap>();
            if (bootstrap == null || !bootstrap.CurrentFloor.HasValue) return null;
            return GameHudView.FloorText(bootstrap.CurrentFloor.Value, bootstrap.Route);
        }
    }
}
