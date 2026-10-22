using System;
using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所（Afterlife）の、燭台の輪の真ん中の台。死んで得たソウルで魂の強化をする所。
    /// 調べると <see cref="Opened"/> を飛ばし、強化の画面（Menu の SoulUpgradeScreen）が開く。
    /// 案内・見出し・本文は、文字の ID（textId）の行がインタラクトの文字の表（Settings/Tables/InteractionTexts.csv）にあればそちらを使う。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Upgrade Altar")]
    public sealed class UpgradeAltar : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("インタラクトの文字の表（InteractionTexts.csv）の ID。行があれば案内・見出し・本文はそちらを使う。")]
        private string textId = "upgrade_altar";

        [SerializeField, Tooltip("プロンプトに出す動詞。")]
        private string promptLabel = "強化する";

        [SerializeField, Tooltip("情報欄の見出し。")]
        private string title = "魂の燭台";

        [SerializeField, TextArea, Tooltip("情報欄の本文。")]
        private string description = "死んで得たソウルで、次の生を強くする。";

        /// <summary>調べられた。強化の画面が受けて開く。</summary>
        public static event Action<UpgradeAltar> Opened;

        public string PromptLabel => InteractionTexts.TryGet(textId, out var entry) ? entry.prompt : promptLabel;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (Opened != null) Opened.Invoke(this);
            else Debug.LogWarning("強化の画面（SoulUpgradeScreen）が無いので開けない", this);
        }

        public string DetailTitle => InteractionTexts.TryGet(textId, out var entry) ? entry.title : title;

        public string DetailBody => InteractionTexts.TryGet(textId, out var entry) ? entry.description : description;

        public Texture2D DetailIcon => null;

        public Color? DetailBackground => null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Opened = null;
    }
}
